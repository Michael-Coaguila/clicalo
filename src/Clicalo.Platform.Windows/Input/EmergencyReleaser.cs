using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Windows.Win32;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The emergency of a hung engine (blueprint §3.2, rule 6; §7.6 «Motor sin latido durante 2 s»; REG-03). It watches
/// the engine's heartbeat in the ledger; after <c>Timings.Engine.EngineStallThreshold</c> without progress it tries to
/// take the gate for <c>Timings.Engine.EmergencyGateWait</c>:
/// <list type="number">
/// <item>taken: the generation goes up, everything recorded is released inside the lock and a new engine is started
/// with the new generation (the old thread, if it ever resumes, is fenced: INV-11);</item>
/// <item>not taken (the hung thread holds the gate, for example inside a <c>SendInput</c> held by third-party hooks):
/// the ledger gets <c>EmergencyRestart</c> and the process ends itself, so Sentinel releases from the ledger and
/// relaunches;</item>
/// <item>a second hang inside <c>Timings.Engine.EngineHangLoop</c> goes straight to the restart.</item>
/// </list>
/// </summary>
/// <remarks>
/// Its checks run on the <see cref="TimeProvider"/>'s timer thread, not on the engine's nor the UI's, so neither can
/// block them. It never touches the engine's state: only the gate and the ledger.
/// </remarks>
public sealed class EmergencyReleaser : IDisposable
{
    /// <summary>The exit code of a process that ends itself because its engine could not be fenced.</summary>
    public const uint EmergencyExitCode = 0xC1C1_0003;

    private readonly InjectionGate _gate;
    private readonly TimeProvider _time;
    private readonly Action<EngineGeneration> _restartEngine;
    private readonly Action _escalate;
    private readonly List<long> _hangs = [];
    private readonly Lock _sync = new();
    private ITimer? _timer;
    private long _lastHeartbeat;
    private long _lastProgress;

    /// <summary>Creates the releaser.</summary>
    /// <param name="gate">The gate of the engine's ledger.</param>
    /// <param name="time">Clock and timer.</param>
    /// <param name="restartEngine">Starts a new engine with the new generation and warns the user (the app).</param>
    /// <param name="escalate">
    /// Ends the process after the ledger got <c>EmergencyRestart</c> (the app writes the crash journal, then
    /// <see cref="TerminateSelf"/>).
    /// </param>
    public EmergencyReleaser(
        InjectionGate gate,
        TimeProvider time,
        Action<EngineGeneration> restartEngine,
        Action escalate
    )
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(restartEngine);
        ArgumentNullException.ThrowIfNull(escalate);
        _gate = gate;
        _time = time;
        _restartEngine = restartEngine;
        _escalate = escalate;
        _lastHeartbeat = gate.Ledger.LastHeartbeatTicks;
        _lastProgress = time.GetTimestamp();
    }

    /// <summary>How many emergencies released and restarted the engine.</summary>
    public int Releases { get; private set; }

    /// <summary>How many emergencies escalated to a process restart.</summary>
    public int Escalations { get; private set; }

    /// <summary>Starts watching: a check every quarter of the stall threshold.</summary>
    public void Start()
    {
        var period = Timings.Engine.EngineStallThreshold / 4;
        _timer ??= _time.CreateTimer(
            static self => ((EmergencyReleaser)self!).Check(),
            this,
            period,
            period
        );
    }

    /// <summary>
    /// One check: when the engine is alive and its heartbeat has not moved for the stall threshold, the emergency
    /// runs. Returns what happened, or <see langword="null"/> when nothing did.
    /// </summary>
    public EmergencyOutcome? Check()
    {
        lock (_sync)
        {
            var heartbeat = _gate.Ledger.LastHeartbeatTicks;
            var now = _time.GetTimestamp();
            if (
                heartbeat != _lastHeartbeat
                || (_gate.Ledger.Marks & LedgerMarks.EngineAlive) == LedgerMarks.None
            )
            {
                _lastHeartbeat = heartbeat;
                _lastProgress = now;
                return null;
            }

            if (_time.GetElapsedTime(_lastProgress, now) < Timings.Engine.EngineStallThreshold)
            {
                return null;
            }

            _lastProgress = now;
            return Trigger(now);
        }
    }

    /// <summary>Ends the current process at once (the escalation of §3.2 rule 6, step 3).</summary>
    public static void TerminateSelf() =>
        PInvoke.TerminateProcess(PInvoke.GetCurrentProcess(), EmergencyExitCode);

    /// <inheritdoc />
    public void Dispose() => _timer?.Dispose();

    private EmergencyOutcome Trigger(long now)
    {
        var loop = Timings.Engine.EngineHangLoop;
        _hangs.RemoveAll(hang => _time.GetElapsedTime(hang, now) > loop.Window);
        _hangs.Add(now);
        if (_hangs.Count < loop.Count)
        {
            var outcome = _gate.TryEmergencyRelease(
                Timings.Engine.EmergencyGateWait,
                out var generation
            );
            if (outcome == EmergencyOutcome.Released)
            {
                Releases++;
                _restartEngine(new EngineGeneration(generation));
                return outcome;
            }
        }

        // The gate is held by the hung thread, or it is the second hang in the window: restart the process.
        Escalations++;
        _gate.Ledger.SetMarks(LedgerMarks.EmergencyRestart);
        _escalate();
        return EmergencyOutcome.GateBusy;
    }
}
