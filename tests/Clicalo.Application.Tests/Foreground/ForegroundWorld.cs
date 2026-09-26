using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Time.Testing;

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

    private ForegroundOrchestrator? _orchestrator;

    public ForegroundWorld(bool seedWord = true)
    {
        Time = TestTime.CreateProvider();
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

    public FakeTimeProvider Time { get; }

    public FakeForegroundControl Control { get; }

    public FakeForegroundMonitor Monitor { get; }

    public FakeSurfaceActivationStyle Surfaces { get; }

    public FakeInternalRightsHotkey Hotkey { get; }

    public FakeInternalKeyEffects Keys { get; }

    /// <summary>Every port call and external event, in order.</summary>
    public List<string> Log { get; } = [];

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

    /// <summary>Acquires a lease that must be granted.</summary>
    public async Task<ForegroundLease> GrantAsync(
        LeaseKind kind,
        WindowToken target,
        LeaseOrigin origin = LeaseOrigin.Touch,
        TimeSpan? idleTimeout = null
    )
    {
        var result = await Orchestrator.AcquireAsync(
            Request(kind, target, origin, idleTimeout),
            TestContext.Current.CancellationToken
        );
        return result.ShouldBeOfType<LeaseResult.Granted>().Lease;
    }

    /// <summary>Acquires a lease that must be denied, and returns why.</summary>
    public async Task<ForegroundDenialReason> DenyAsync(
        LeaseKind kind,
        WindowToken target,
        LeaseOrigin origin
    )
    {
        var result = await Orchestrator.AcquireAsync(
            Request(kind, target, origin),
            TestContext.Current.CancellationToken
        );
        return result.ShouldBeOfType<LeaseResult.Denied>().Reason;
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
