using System.Collections.Immutable;
using Clicalo.Application.Engine;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// An engine host over fake ports and a fake clock. By default its reducer is a script: it records the events it gets
/// and answers with the effects the test queues, so the host's interpretation is tested on its own; with
/// <c>realReducer</c> it runs the product's <see cref="EngineReducer"/>.
/// </summary>
internal sealed class HostWorld : IDisposable
{
    public static readonly InjectedKey Shift = new(0xA0, 0x2A, false, InjectionMode.VirtualKey);
    public static readonly InjectedKey Ctrl = new(0xA2, 0x1D, false, InjectionMode.VirtualKey);

    public static readonly EngineConfig Config = new(
        TimeSpan.FromSeconds(60),
        ReleaseOnAppSwitch: true,
        TimeSpan.FromMilliseconds(20),
        new TouchSettings(TimeSpan.FromMilliseconds(250), 8, 24, TimeSpan.Zero)
    );

    public HostWorld(
        EngineState? initial = null,
        bool realReducer = false,
        Func<EngineGeneration, bool>? releaseRecorded = null
    )
    {
        Ports = new EngineHostPorts(Injector, Ledger, Shell, Shell, Observer) { ReleaseRecorded = releaseRecorded };
        Host = new EngineHost(
            Ports,
            Generation,
            Config,
            Time,
            NullLogger<EngineHost>.Instance,
            realReducer ? EngineReducer.Reduce : Script,
            initial ?? WithForeground(EngineState.Empty)
        );
    }

    public static EngineGeneration Generation { get; } = new(7);

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));

    public FakeInjector Injector { get; } = new();

    public FakeLedger Ledger { get; } = new();

    public FakeShell Shell { get; } = new();

    public FakeObserver Observer { get; } = new();

    public EngineHostPorts Ports { get; }

    public EngineHost Host { get; }

    public List<EngineEvent> Seen { get; } = [];

    /// <summary>The effects the scripted reducer answers the next event with.</summary>
    public Queue<ImmutableArray<EngineEffect>> Answers { get; } = new();

    /// <summary>When set, the scripted reducer throws for this event type.</summary>
    public Type? ThrowOn { get; set; }

    public static ForegroundInfo Notepad { get; } =
        new(
            new ForegroundWindowId(0x1234),
            new ProcessName("notepad"),
            Epoch: 3,
            ElevationState.Allowed,
            KeyboardLayoutSnapshot.Empty
        );

    public static EngineState WithForeground(EngineState state) => state with { Foreground = Notepad };

    /// <summary>A state whose ledger holds Shift for a contact.</summary>
    public static EngineState HoldingShift()
    {
        var item = new PressedItem(
            HolderId.ForContact(1),
            HoldOrigin.Contact,
            new ShortcutId("shift"),
            1,
            [Shift],
            MouseButtons.None,
            0,
            null
        );
        return WithForeground(EngineState.Empty with { Keys = KeyboardLedger.Empty.Acquire(item).Ledger });
    }

    public static EngineEffect.Inject Press(long epoch = 3, params InjectedEvent[] events) =>
        new(events.Length == 0 ? [InjectedEvent.KeyDown(Ctrl)] : [.. events], epoch, null, IsRelease: false, IsInternal: false)
        {
            Effect = new EffectId(42),
        };

    public static EngineEffect.Inject Release(params InjectedEvent[] events) =>
        new(events.Length == 0 ? [InjectedEvent.KeyUp(Ctrl)] : [.. events], null, null, IsRelease: true, IsInternal: false);

    /// <summary>Queues an event and processes the mailbox once.</summary>
    public void Handle(EngineEvent engineEvent, params EngineEffect[] answer)
    {
        Answers.Enqueue([.. answer]);
        Host.Post(engineEvent);
        Host.Pump();
    }

    public void Dispose() => Host.Dispose();

    private EngineTransition Script(EngineState state, EngineEvent engineEvent, EngineConfig config, long now)
    {
        Seen.Add(engineEvent);
        if (ThrowOn is { } type && type.IsInstanceOfType(engineEvent))
        {
            throw new InvalidOperationException("scripted failure");
        }

        var effects = Answers.Count > 0 ? Answers.Dequeue() : [];
        var next = engineEvent switch
        {
            EngineEvent.SetTestMode testMode => state with { TestMode = testMode.On },
            _ => state,
        };
        return new EngineTransition(next with { Version = state.Version + 1 }, effects);
    }
}
