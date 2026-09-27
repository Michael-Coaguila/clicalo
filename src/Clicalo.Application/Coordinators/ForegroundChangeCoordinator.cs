using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Coordinators;

/// <summary>
/// Step 1 of the app switch of blueprint §7.9: every verified external foreground the monitor reports becomes an
/// <see cref="EngineEvent.ForegroundChanged"/> with a new epoch, the process, the elevation state (EJE-013) and the
/// keyboard layout. The engine releases everything on a real switch when <c>safeSwitch</c> is on (SEG-005) and refuses
/// planned injections of an older epoch (INV-6); the panel stamps its activations with <see cref="CurrentEpoch"/>.
/// </summary>
/// <remarks>
/// A real switch is a change of the app the user sees. Coming back to the same app after a lease (the tray menu, the
/// Control Center) is reported by the monitor as a change, since Clícalo was in front in between, but it keeps what is
/// held: it gets a new epoch and <c>isUserSwitch = false</c>. The monitor already leaves out Clícalo's own windows, the
/// shell, the touch keyboard and Voice access. «Try now» (PRB-006) joins in M4. Events arrive on the SysEvents thread;
/// <see cref="Start"/> may run on any thread.
/// </remarks>
public sealed class ForegroundChangeCoordinator : IDisposable
{
    private readonly IForegroundMonitor _monitor;
    private readonly IEngineInbox _engine;
    private readonly Func<ExternalForeground, ForegroundDetails> _describe;
    private readonly bool _selfElevated;
    private readonly Lock _gate = new();
    private ExternalForeground? _last;
    private long _epoch;
    private bool _started;

    /// <summary>Creates the coordinator.</summary>
    /// <param name="monitor">The verified external foreground.</param>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="describe">Resolves the process and the keyboard layout (Platform.Windows).</param>
    /// <param name="selfElevated">Whether Clícalo runs elevated: an elevated app then accepts its input (EJE-013).</param>
    public ForegroundChangeCoordinator(
        IForegroundMonitor monitor,
        IEngineInbox engine,
        Func<ExternalForeground, ForegroundDetails> describe,
        bool selfElevated
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(describe);
        _monitor = monitor;
        _engine = engine;
        _describe = describe;
        _selfElevated = selfElevated;
    }

    /// <summary>The epoch of the last foreground posted to the engine; zero before the first one.</summary>
    public long CurrentEpoch => Interlocked.Read(ref _epoch);

    /// <summary>Starts following the monitor and posts the foreground it already knows.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _monitor.ExternalForegroundChanged += OnExternalForegroundChanged;
            if (_monitor.Current is { } current)
            {
                Publish(current);
            }
        }
    }

    /// <summary>
    /// Posts the foreground posted last again, with a new epoch and as no switch of app: an engine that replaced a hung
    /// one after an emergency (blueprint §3.2 rule 6) starts empty and would refuse every activation until the next
    /// change of foreground (INV-6).
    /// </summary>
    public void Republish()
    {
        lock (_gate)
        {
            if (_last is { } last)
            {
                Post(last, isUserSwitch: false);
            }
        }
    }

    /// <summary>Stops following the monitor.</summary>
    public void Dispose() => _monitor.ExternalForegroundChanged -= OnExternalForegroundChanged;

    /// <summary>
    /// Maps the elevation the monitor read to what the engine obeys (EJE-013): an elevated app blocks a medium Clícalo
    /// through UIPI; an unreadable one is tried and warned about if sending fails (EC-PER-03).
    /// </summary>
    /// <param name="target">The foreground process.</param>
    /// <param name="selfElevated">Whether Clícalo runs elevated.</param>
    public static ElevationState ElevationOf(ProcessElevation target, bool selfElevated) =>
        target switch
        {
            ProcessElevation.Elevated when !selfElevated => ElevationState.TargetElevated,
            ProcessElevation.Unknown => ElevationState.Unknown,
            _ => ElevationState.Allowed,
        };

    private void OnExternalForegroundChanged(
        object? sender,
        ExternalForegroundChangedEventArgs change
    )
    {
        lock (_gate)
        {
            Publish(change.Foreground);
        }
    }

    private void Publish(ExternalForeground foreground)
    {
        // A duplicate report of the window already posted (the monitor seeds Current and may raise it again) changes
        // nothing for the engine.
        if (
            _last is { } last
            && last.Window == foreground.Window
            && last.ObservedAt == foreground.ObservedAt
        )
        {
            return;
        }

        var isUserSwitch = _last is not null && _last.AppProcessId != foreground.AppProcessId;
        _last = foreground;
        Post(foreground, isUserSwitch);
    }

    private void Post(ExternalForeground foreground, bool isUserSwitch)
    {
        var details = _describe(foreground);
        var epoch = Interlocked.Increment(ref _epoch);
        _ = _engine.Post(
            new EngineEvent.ForegroundChanged(
                new ForegroundInfo(
                    new ForegroundWindowId(unchecked((ulong)foreground.Window.Handle)),
                    details.Process,
                    epoch,
                    ElevationOf(foreground.Elevation, _selfElevated),
                    details.Layout
                ),
                isUserSwitch
            )
        );
    }
}
