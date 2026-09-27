using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>
/// A pure model of the engine host for the reducer's tests: it feeds events to <see cref="EngineReducer"/>, keeps the
/// timers the effects ask for, fires them when the clock moves and applies every injected batch to a
/// <see cref="Receiver"/>. Time is in <see cref="TimeSpan"/> ticks (the fake time provider's frequency).
/// </summary>
internal sealed class EngineHarness
{
    public static readonly DateTimeOffset Start = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    public static readonly TouchSettings Touch = new(
        TimeSpan.FromMilliseconds(250),
        HitSlopPx: 8,
        CancelMovePx: 24,
        MinContact: TimeSpan.Zero
    );

    public static EngineConfig DefaultConfig { get; } =
        new(
            MaxHold: TimeSpan.FromSeconds(60),
            ReleaseOnAppSwitch: true,
            InterEventDelay: TimeSpan.FromMilliseconds(20),
            Touch
        );

    public EngineHarness(EngineConfig? config = null, InjectionMode mode = InjectionMode.VirtualKey)
    {
        Config = config ?? DefaultConfig;
        Mode = mode;
    }

    public EngineState State { get; private set; } = EngineState.Empty;

    public EngineConfig Config { get; private set; }

    public InjectionMode Mode { get; set; }

    public long Now { get; private set; }

    public DateTimeOffset Clock => Start + TimeSpan.FromTicks(Now);

    public Receiver Receiver { get; } = new();

    public Dictionary<TimerKey, long> Timers { get; } = [];

    public List<EngineEffect> Effects { get; } = [];

    public IReadOnlyList<EngineEffect> LastEffects { get; private set; } = [];

    public long Epoch { get; private set; }

    public ForegroundWindowId Window => new((ulong)(0x1000 + Epoch));

    public PhysicalPoint? Pointer { get; set; } = new PhysicalPoint(500, 400);

    /// <summary>Feeds one event, interprets its effects and returns them.</summary>
    public IReadOnlyList<EngineEffect> Apply(EngineEvent engineEvent)
    {
        if (engineEvent is EngineEvent.ConfigChanged changed)
        {
            Config = changed.Config;
        }

        var transition = EngineReducer.Reduce(State, engineEvent, Config, Now);
        State = transition.Next;
        LastEffects = transition.Effects;
        Effects.AddRange(transition.Effects);
        var failures = new List<EffectId>();
        var refused = new List<InjectedEvent>();
        foreach (var effect in transition.Effects)
        {
            switch (effect)
            {
                case EngineEffect.Inject { IsRelease: true } release when RefuseReleases:
                    // The secure desktop is in front: SendInput refuses the batch whole (ERROR_ACCESS_DENIED).
                    refused.AddRange(release.Events);
                    break;
                case EngineEffect.Inject { IsRelease: false } press when FailNextPress is { } taken:
                    // SendInput took only the first events of the batch; the host reports it (INV-5).
                    FailNextPress = null;
                    FailedPresses++;
                    Receiver.Tolerate(press.Events);
                    Receiver.Apply(press.Events.Take(Math.Min(taken, press.Events.Length - 1)));
                    failures.Add(press.Effect);
                    break;
                case EngineEffect.Inject inject:
                    Receiver.Apply(inject.Events);
                    break;
                case EngineEffect.Schedule schedule:
                    Timers[schedule.Key] = schedule.DueTicks;
                    break;
                case EngineEffect.CancelTimer cancel:
                    Timers.Remove(cancel.Key);
                    break;
            }
        }

        foreach (var failure in failures)
        {
            Apply(new EngineEvent.InjectFailed(failure, Win32Error: 5));
        }

        if (refused.Count > 0)
        {
            Apply(new EngineEvent.ReleasesBlocked([.. refused]));
        }

        return transition.Effects;
    }

    /// <summary>
    /// When set, the next press batch is taken only in part (that many events, at most all but the last) and the
    /// host reports <see cref="EngineEvent.InjectFailed"/>.
    /// </summary>
    public int? FailNextPress { get; set; }

    /// <summary>
    /// While set, the secure desktop (UAC, Ctrl+Alt+Del, the lock screen) is in front: every release batch is refused
    /// whole and comes back as <see cref="EngineEvent.ReleasesBlocked"/>, as the host reports it.
    /// </summary>
    public bool RefuseReleases { get; set; }

    /// <summary>How many press batches were taken only in part so far.</summary>
    public int FailedPresses { get; private set; }

    /// <summary>Whether scenario steps may make a press batch fail (off to replay exactly what was sent).</summary>
    public bool SimulateFailures { get; set; } = true;

    /// <summary>Moves the clock, firing every timer that falls due on the way, in order.</summary>
    public void Advance(TimeSpan duration) => AdvanceTo(Now + duration.Ticks);

    public void AdvanceTo(long target)
    {
        while (
            Timers.Where(t => t.Value <= target).OrderBy(static t => t.Value).FirstOrDefault()
                is { Key.Value: not null } due
        )
        {
            Now = Math.Max(Now, due.Value);
            Timers.Remove(due.Key);
            Apply(new EngineEvent.TimerFired(due.Key));
        }

        Now = Math.Max(Now, target);
    }

    /// <summary>Lets the outbox and every short timer finish (a Tap takes a few pauses).</summary>
    public void Settle() => Advance(TimeSpan.FromMilliseconds(500));

    /// <summary>A verified foreground change; a real switch unless said otherwise.</summary>
    public IReadOnlyList<EngineEffect> Foreground(
        string process = "notepad",
        ElevationState elevation = ElevationState.Allowed,
        bool userSwitch = true,
        KeyboardLayoutSnapshot? layout = null
    )
    {
        Epoch++;
        return Apply(
            new EngineEvent.ForegroundChanged(
                new ForegroundInfo(
                    Window,
                    new ProcessName(process),
                    Epoch,
                    elevation,
                    layout ?? Layouts.Spanish
                ),
                userSwitch
            )
        );
    }

    public EngineEvent.Activation Activation(
        Shortcut shortcut,
        ActivationPhase phase,
        int? contact,
        TimeSpan? duration = null,
        long? epoch = null,
        bool editMode = false
    ) =>
        new(
            new ActivationRequest(
                phase,
                phase == ActivationPhase.Invoke
                    ? ActivationOrigin.UiaInvoke
                    : ActivationOrigin.Touch,
                phase == ActivationPhase.Invoke ? null : contact,
                phase == ActivationPhase.ContactEnded
                    ? new ContactSummary(
                        duration ?? TimeSpan.FromMilliseconds(120),
                        0,
                        PalmLike: false
                    )
                    : null,
                Clock
            ),
            shortcut,
            OriginProfile: null,
            Mode,
            Pointer,
            editMode,
            epoch ?? Epoch,
            RequiredForeground: null
        );

    /// <summary>A finger taps the tile (the decision is taken when it lifts).</summary>
    public IReadOnlyList<EngineEffect> Tap(
        Shortcut shortcut,
        int contact = 1,
        long? epoch = null
    ) => Apply(Activation(shortcut, ActivationPhase.ContactEnded, contact, epoch: epoch));

    /// <summary>Voice, keyboard or switch invokes the tile (no contact, no filter, EJE-005).</summary>
    public IReadOnlyList<EngineEffect> Invoke(Shortcut shortcut) =>
        Apply(Activation(shortcut, ActivationPhase.Invoke, contact: null));

    /// <summary>A finger rests on the tile (a Hold presses here).</summary>
    public IReadOnlyList<EngineEffect> Press(Shortcut shortcut, int contact = 1) =>
        Apply(Activation(shortcut, ActivationPhase.ContactStarted, contact));

    /// <summary>The finger lifts (or the contact is cancelled).</summary>
    public IReadOnlyList<EngineEffect> Lift(int contact = 1, bool cancelled = false) =>
        Apply(
            new EngineEvent.ContactEnded(
                contact,
                new ContactSummary(TimeSpan.FromMilliseconds(300), 0, PalmLike: false),
                cancelled
            )
        );

    /// <summary>Every injected event so far, in order.</summary>
    public IEnumerable<InjectedEvent> Sent =>
        Effects.OfType<EngineEffect.Inject>().SelectMany(static i => i.Events);
}
