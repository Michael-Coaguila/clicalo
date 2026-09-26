using System.ComponentModel;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.KeyLedger;
using Microsoft.Extensions.Logging;

namespace Clicalo.Platform.Windows.SentinelHost;

/// <summary>
/// Keeps <c>Clicalo.Sentinel.exe</c> alive next to the main process (blueprint §3.1, ADR-0004, ADR-0018): starts it
/// with exactly three inherited handles (the main process, the ledger read-only and the read end of an anonymous
/// pipe) through <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c>, writes a heartbeat byte every
/// <c>Timings.Guardian.PipeHeartbeatInterval</c>, and when the pipe breaks (Sentinel died) starts it again after
/// <c>Timings.Guardian.RestartBackoff</c>. Past <c>Timings.Guardian.RestartLoop</c> it stops and raises
/// <see cref="GuardianUnstable"/> (Sistema › Inicio y estabilidad).
/// </summary>
/// <remarks>
/// Started from a background thread early in the start-up, in parallel with the first frame; the engine never waits
/// for it (§3.1). <see cref="Stop"/> closes the pipe: Sentinel sees its parent alive and leaves without releasing.
/// </remarks>
public sealed partial class SentinelSupervisor : IDisposable
{
    private readonly KeyLedgerSection _ledger;
    private readonly string _sentinelPath;
    private readonly TimeProvider _time;
    private readonly ILogger<SentinelSupervisor> _logger;
    private readonly Lock _sync = new();
    private readonly List<long> _deaths = [];
    private GuardianProcess? _process;
    private nint _pipe;
    private ITimer? _heartbeat;
    private ITimer? _restart;
    private int _attempt;
    private bool _stopped;

    /// <summary>Creates the supervisor.</summary>
    /// <param name="ledger">The engine's ledger (duplicated read-only for Sentinel).</param>
    /// <param name="sentinelPath">Full path of <c>Clicalo.Sentinel.exe</c>, next to <c>Clicalo.exe</c>.</param>
    /// <param name="time">Clock and timers.</param>
    /// <param name="logger">Logs codes only.</param>
    public SentinelSupervisor(
        KeyLedgerSection ledger,
        string sentinelPath,
        TimeProvider time,
        ILogger<SentinelSupervisor> logger
    )
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentException.ThrowIfNullOrEmpty(sentinelPath);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _ledger = ledger;
        _sentinelPath = sentinelPath;
        _time = time;
        _logger = logger;
    }

    /// <summary>Raised when Sentinel died <c>Timings.Guardian.RestartLoop</c> times: it is no longer restarted.</summary>
    public event EventHandler? GuardianUnstable;

    /// <summary>Sentinel's process id while it runs.</summary>
    public int? ProcessId
    {
        get
        {
            lock (_sync)
            {
                return _process is { HasExited: false } process ? process.Id : null;
            }
        }
    }

    /// <summary>How many times Sentinel was started.</summary>
    public int Launches { get; private set; }

    /// <summary>The start-up contract given to the last Sentinel (tests).</summary>
    public SentinelStartInfo? LastStartInfo { get; private set; }

    /// <summary>Starts Sentinel and the heartbeat.</summary>
    public void Start()
    {
        lock (_sync)
        {
            _stopped = false;
            Launch();
            var interval = Timings.Guardian.PipeHeartbeatInterval;
            _heartbeat ??= _time.CreateTimer(
                static self => ((SentinelSupervisor)self!).Beat(),
                this,
                interval,
                interval
            );
        }
    }

    /// <summary>
    /// Stops supervising and closes the pipe, so Sentinel leaves without releasing (the main process has released
    /// everything and set its marks before).
    /// </summary>
    public void Stop()
    {
        lock (_sync)
        {
            _stopped = true;
            _heartbeat?.Dispose();
            _heartbeat = null;
            _restart?.Dispose();
            _restart = null;
            ClosePipe();
            _process?.Dispose();
            _process = null;
        }
    }

    /// <summary>One heartbeat: a byte down the pipe; a broken pipe means Sentinel died.</summary>
    internal void Beat()
    {
        lock (_sync)
        {
            if (_stopped || _process is null)
            {
                return;
            }

            if (!_process.HasExited && GuardianHandles.WriteHeartbeat(_pipe))
            {
                return;
            }

            OnDied();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();

    private void Launch()
    {
        var parent = GuardianHandles.DuplicateCurrentProcessForChild();
        var ledger = _ledger.DuplicateForGuardian();
        var (read, write) = GuardianHandles.CreateHeartbeatPipe();
        try
        {
            var crashLoop = Timings.App.CrashLoop;
            var info = new SentinelStartInfo(
                parent,
                ledger,
                read,
                Timings.Guardian.PipeHeartbeatInterval,
                crashLoop.Count,
                crashLoop.Window
            );
            _process = GuardianProcess.Start(
                _sentinelPath,
                info.ToArguments(),
                info.InheritedHandles.AsSpan()
            );
            _pipe = write;
            LastStartInfo = info;
            Launches++;
            LogStarted(_logger, _process.Id);
        }
        catch (Win32Exception ex)
        {
            GuardianHandles.Close(write);
            LogLaunchFailed(_logger, ex.NativeErrorCode);
            ScheduleRestart();
        }
        finally
        {
            // Sentinel has its own copies now; ours are closed.
            GuardianHandles.Close(parent);
            GuardianHandles.Close(ledger);
            GuardianHandles.Close(read);
        }
    }

    private void OnDied()
    {
        LogDied(_logger);
        ClosePipe();
        _process?.Dispose();
        _process = null;
        ScheduleRestart();
    }

    private void ScheduleRestart()
    {
        var now = _time.GetTimestamp();
        var loop = Timings.Guardian.RestartLoop;
        _deaths.RemoveAll(death => _time.GetElapsedTime(death, now) > loop.Window);
        _deaths.Add(now);
        if (_deaths.Count >= loop.Count)
        {
            _stopped = true;
            LogUnstable(_logger, _deaths.Count);
            GuardianUnstable?.Invoke(this, EventArgs.Empty);
            return;
        }

        var backoff = Timings.Guardian.RestartBackoff;
        var wait = backoff[Math.Min(_attempt, backoff.Length - 1)];
        _attempt++;
        _restart?.Dispose();
        _restart = _time.CreateTimer(
            static self => ((SentinelSupervisor)self!).Restart(),
            this,
            wait,
            Timeout.InfiniteTimeSpan
        );
    }

    private void Restart()
    {
        lock (_sync)
        {
            if (_stopped || _process is not null)
            {
                return;
            }

            Launch();
        }
    }

    private void ClosePipe()
    {
        GuardianHandles.Close(_pipe);
        _pipe = 0;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "guardian.started: process {ProcessId}"
    )]
    private static partial void LogStarted(ILogger logger, int processId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "guardian.launch_failed: Win32 error {Error}"
    )]
    private static partial void LogLaunchFailed(ILogger logger, int error);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "guardian.died: restarting after the backoff"
    )]
    private static partial void LogDied(ILogger logger);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "guardian.unstable: {Deaths} deaths inside the window; no longer restarted"
    )]
    private static partial void LogUnstable(ILogger logger, int deaths);
}
