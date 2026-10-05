using System.ComponentModel;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Core.Guardian;
using Microsoft.Extensions.Logging;

namespace Clicalo.Platform.Windows.SentinelHost;

/// <summary>
/// Keeps <c>Clicalo.Sentinel.exe</c> alive next to the main process (blueprint §3.1, ADR-0022): starts it with exactly
/// one inherited handle (the main process) through <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c>, checks every
/// <c>Timings.Guardian.WatchInterval</c> that it still runs, and when it died starts it again after
/// <c>Timings.Guardian.RestartBackoff</c>. Past <c>Timings.Guardian.RestartLoop</c> it stops and raises
/// <see cref="GuardianUnstable"/> (Sistema › Inicio y estabilidad).
/// </summary>
/// <remarks>
/// Started from a background thread early in the start-up, in parallel with the first frame; the engine never waits
/// for it (§3.1). <see cref="Stop"/> only stops supervising: Sentinel stays until the main process ends, releases what
/// is down then, and relaunches only after an abnormal exit.
/// </remarks>
public sealed partial class SentinelSupervisor : IDisposable
{
    private readonly Func<SentinelStartInfo, ISentinelChild> _launch;
    private readonly TimeProvider _time;
    private readonly ILogger<SentinelSupervisor> _logger;
    private readonly Lock _sync = new();
    private readonly List<long> _deaths = [];
    private ISentinelChild? _child;
    private ITimer? _watch;
    private ITimer? _restart;
    private int _attempt;
    private bool _stopped;

    /// <summary>Creates the supervisor.</summary>
    /// <param name="sentinelPath">Full path of <c>Clicalo.Sentinel.exe</c>, next to <c>Clicalo.exe</c>.</param>
    /// <param name="time">Clock and timers.</param>
    /// <param name="logger">Logs codes only.</param>
    public SentinelSupervisor(
        string sentinelPath,
        TimeProvider time,
        ILogger<SentinelSupervisor> logger
    )
        : this(info => SentinelProcess.Start(sentinelPath, info), time, logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(sentinelPath);
    }

    /// <summary>Creates the supervisor over another launcher (tests).</summary>
    internal SentinelSupervisor(
        Func<SentinelStartInfo, ISentinelChild> launch,
        TimeProvider time,
        ILogger<SentinelSupervisor> logger
    )
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _launch = launch;
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
                return _child is { HasExited: false } child ? child.Id : null;
            }
        }
    }

    /// <summary>How many times Sentinel was started.</summary>
    public int Launches { get; private set; }

    /// <summary>The start-up contract given to the last Sentinel (tests).</summary>
    public SentinelStartInfo? LastStartInfo { get; private set; }

    /// <summary>Starts Sentinel and the watch.</summary>
    public void Start()
    {
        lock (_sync)
        {
            _stopped = false;
            Launch();
            var interval = Timings.Guardian.WatchInterval;
            _watch ??= _time.CreateTimer(
                static self => ((SentinelSupervisor)self!).Check(),
                this,
                interval,
                interval
            );
        }
    }

    /// <summary>Stops supervising; Sentinel itself goes on until the main process ends.</summary>
    public void Stop()
    {
        lock (_sync)
        {
            _stopped = true;
            _watch?.Dispose();
            _watch = null;
            _restart?.Dispose();
            _restart = null;
            _child?.Dispose();
            _child = null;
        }
    }

    /// <summary>One check: a Sentinel that ended is started again after the backoff.</summary>
    internal void Check()
    {
        lock (_sync)
        {
            if (_stopped || _child is null || !_child.HasExited)
            {
                return;
            }

            LogDied(_logger);
            _child.Dispose();
            _child = null;
            ScheduleRestart();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();

    private void Launch()
    {
        nint parent = 0;
        try
        {
            parent = GuardianHandles.DuplicateCurrentProcessForChild();
            var crashLoop = Timings.App.CrashLoop;
            var info = new SentinelStartInfo(
                parent,
                Timings.Guardian.ReleaseRetryInterval,
                crashLoop.Count,
                crashLoop.Window
            );
            _child = _launch(info);
            LastStartInfo = info;
            Launches++;
            LogStarted(_logger, _child.Id);
        }
        catch (Win32Exception ex)
        {
            LogLaunchFailed(_logger, ex.NativeErrorCode);
            ScheduleRestart();
        }
        finally
        {
            // Sentinel has its own copy now; ours is closed.
            GuardianHandles.Close(parent);
        }
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
            if (_stopped || _child is not null)
            {
                return;
            }

            Launch();
        }
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
