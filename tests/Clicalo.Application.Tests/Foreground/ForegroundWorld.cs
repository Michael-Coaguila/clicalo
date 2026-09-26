using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Time;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// A small desktop for <see cref="ForegroundOrchestrator"/>: fake ports that share one log, a fake clock and a few
/// named windows. Word is the external app in front when the test starts; Clícalo holds no foreground right.
/// </summary>
internal sealed class ForegroundWorld : IDisposable
{
    public static readonly WindowToken Word = new(0x101);
    public static readonly WindowToken Notepad = new(0x102);
    public static readonly WindowToken Chrome = new(0x103);
    public static readonly WindowToken Search = new(0x201);
    public static readonly WindowToken Panel = new(0x202);
    public static readonly WindowToken ControlCenter = new(0x301);
    public static readonly WindowToken TrayHost = new(0x302);

    public static readonly SurfaceId SearchSurface = new(SurfaceKind.Panel, 0);
    public static readonly SurfaceId PanelSurface = new(SurfaceKind.Panel, 1);

    private static readonly TimeSpan ContinuationGrace = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    private ForegroundOrchestrator? _orchestrator;

    public ForegroundWorld(bool seedWord = true)
    {
        Time = new WatchedTime(TestTime.Epoch);
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Control = new FakeForegroundControl(this);
        Monitor = new FakeForegroundMonitor(this);
        Surfaces = new FakeSurfaceActivationStyle(this);
        Hotkey = new FakeInternalRightsHotkey(this);
        Keys = new FakeInternalKeyEffects(this);
        Surfaces.Register(Search, SearchSurface);
        Surfaces.Register(Panel, PanelSurface);
        if (seedWord)
        {
            Monitor.Seed(Word);
        }
    }

    public WatchedTime Time { get; }

    public FakeForegroundControl Control { get; }

    public FakeForegroundMonitor Monitor { get; }

    public FakeSurfaceActivationStyle Surfaces { get; }

    public FakeInternalRightsHotkey Hotkey { get; }

    public FakeInternalKeyEffects Keys { get; }

    /// <summary>Every port call and external event, in order. Read it once the operation under test has ended.</summary>
    public List<string> Log { get; } = [];

    /// <summary>
    /// Runs inside every port call, with its log entry: a test observes on which thread, and when, the orchestrator
    /// touches the ports.
    /// </summary>
    public Action<string>? OnPortCall { get; set; }

    /// <summary>Appends to <see cref="Log"/>; the orchestrator works on the thread pool, so writes are serialized.</summary>
    public void Write(string entry)
    {
        lock (Log)
        {
            Log.Add(entry);
        }

        Observe(entry);
    }

    /// <summary>A port call that is not logged (a query such as <c>GetForegroundWindow</c>) for <see cref="OnPortCall"/>.</summary>
    public void Observe(string call) => OnPortCall?.Invoke(call);

    public ForegroundOrchestrator Orchestrator =>
        _orchestrator ??= new ForegroundOrchestrator(
            new ForegroundPorts
            {
                Control = Control,
                Monitor = Monitor,
                SurfaceStyle = Surfaces,
                Surfaces = Surfaces,
                RightsHotkey = Hotkey,
                KeyEffects = Keys,
            },
            Time
        );

    public static LeaseRequest Request(
        LeaseKind kind,
        WindowToken target,
        LeaseOrigin origin,
        TimeSpan? idleTimeout = null
    ) => new(kind, target, origin, idleTimeout);

    /// <summary>Acquires a lease that must be granted, moving the clock through every verification wait.</summary>
    public async Task<ForegroundLease> GrantAsync(
        LeaseKind kind,
        WindowToken target,
        LeaseOrigin origin = LeaseOrigin.Touch,
        TimeSpan? idleTimeout = null
    )
    {
        var result = await AcquireAsync(Request(kind, target, origin, idleTimeout));
        return result.ShouldBeOfType<LeaseResult.Granted>().Lease;
    }

    /// <summary>Acquires a lease that must be denied, and returns why.</summary>
    public async Task<ForegroundDenialReason> DenyAsync(
        LeaseKind kind,
        WindowToken target,
        LeaseOrigin origin
    )
    {
        var result = await AcquireAsync(Request(kind, target, origin));
        return result.ShouldBeOfType<LeaseResult.Denied>().Reason;
    }

    /// <summary>
    /// Runs <paramref name="pending"/> to the end, advancing the fake clock by <c>RestoreRetryDelay</c> while it
    /// waits (each attempt is verified again after that delay), at most <paramref name="maxWaits"/> times.
    /// </summary>
    public async Task<T> CompleteAsync<T>(Task<T> pending, int maxWaits = 6)
    {
        for (var wait = 0; wait < maxWaits && !pending.IsCompleted; wait++)
        {
            // Continuations that hop to the thread pool (cancellation callbacks, the gate) run in real time.
            _ = await Task.WhenAny(pending, Task.Delay(ContinuationGrace));
            if (!pending.IsCompleted)
            {
                Time.Advance(Timings.Foreground.RestoreRetryDelay);
            }
        }

        return await pending.WaitAsync(OperationTimeout);
    }

    private Task<LeaseResult> AcquireAsync(LeaseRequest request) =>
        CompleteAsync(
            Orchestrator.AcquireAsync(request, TestContext.Current.CancellationToken).AsTask()
        );

    /// <summary>
    /// Starts <paramref name="operation"/> and returns it once it has armed its next timer (the verification delay after
    /// a refused attempt) or ended: the orchestrator continues on the thread pool, so the test must not move the clock
    /// before the delay exists.
    /// </summary>
    public async Task<TTask> StartUntilItWaitsAsync<TTask>(Func<TTask> operation)
        where TTask : Task
    {
        var timers = Time.TimersCreated;
        var pending = operation();
        _ = await Task.WhenAny(pending, Time.WhenTimersAsync(timers + 1));
        return pending;
    }

    /// <summary>Clears the log, to look only at what happens next.</summary>
    public void Mark() => Log.Clear();

    /// <summary>True for Clícalo's own windows (surfaces, Control Center, tray host).</summary>
    public static bool IsOwn(WindowToken window) =>
        window == Search || window == Panel || window == ControlCenter || window == TrayHost;

    public static string Name(WindowToken window) =>
        window switch
        {
            _ when window == Word => nameof(Word),
            _ when window == Notepad => nameof(Notepad),
            _ when window == Chrome => nameof(Chrome),
            _ when window == Search => nameof(Search),
            _ when window == Panel => nameof(Panel),
            _ when window == ControlCenter => nameof(ControlCenter),
            _ when window == TrayHost => nameof(TrayHost),
            _ => window.ToString(),
        };

    public void Dispose() => _orchestrator?.Dispose();
}
