using System.Collections.Immutable;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// One reduction of the engine in progress: the state so far and the effects produced so far. Local to one call of
/// <see cref="EngineReducer.Reduce(EngineState, EngineEvent, EngineConfig, long)"/>, so the reducer stays a pure function. Holds the operations every planner
/// shares: the outbox, safety releases, cancellation of a holder and the timers.
/// </summary>
internal sealed class EngineStep
{
    private readonly ImmutableArray<EngineEffect>.Builder _effects =
        ImmutableArray.CreateBuilder<EngineEffect>();
    private bool _advancingMacro;

    public EngineStep(EngineState state, EngineConfig config, long nowTicks)
    {
        State = state;
        Config = config;
        Now = nowTicks;
    }

    public EngineState State { get; set; }

    public EngineConfig Config { get; }

    public long Now { get; }

    public KeyboardLayoutSnapshot Layout =>
        State.Foreground?.Layout ?? KeyboardLayoutSnapshot.Empty;

    public ImmutableArray<EngineEffect> Effects => _effects.ToImmutable();

    public void Emit(EngineEffect effect) => _effects.Add(effect);

    public void Notice(Message text, NoticeUrgency urgency = NoticeUrgency.Polite) =>
        Emit(new EngineEffect.Notice(text, urgency));

    public long NextSequence()
    {
        var next = State.Sequence + 1;
        State = State with { Sequence = next };
        return next;
    }

    public long Ticks(TimeSpan duration) =>
        KeyboardLedger.ToTicks(duration, Config.TimestampFrequency);

    /// <summary>The deadline of a held item under <paramref name="limit"/> (SEG-004, INV-4).</summary>
    public (long? Deadline, bool InheritsGlobal) Deadline(HoldLimit limit) =>
        limit switch
        {
            HoldLimit.After after => (Now + Ticks(after.Limit), false),
            HoldLimit.Never => (null, false),
            _ => GlobalDeadline(),
        };

    public (long? Deadline, bool InheritsGlobal) GlobalDeadline() =>
        (Config.MaxHold is { } max ? Now + Ticks(max) : null, true);

    /// <summary>
    /// Whether presses planned for <paramref name="origin"/> may go now (INV-6, INV-7): not in test mode or pause, the
    /// foreground epoch and window still match, and the target is not elevated above Clícalo.
    /// </summary>
    public bool CanPress(ExecutionOrigin origin) =>
        !State.TestMode
        && !State.Paused
        && State.Foreground is { } foreground
        && foreground.Epoch == origin.Epoch
        && (origin.RequiredForeground is null || origin.RequiredForeground == foreground.Window)
        && foreground.Elevation != ElevationState.TargetElevated;

    /// <summary>Sends the events of a release transition at once, never filtered (INV-8).</summary>
    public void Release(LedgerTransition transition, HolderId? holder)
    {
        State = State with { Keys = transition.Ledger };
        if (!transition.Events.IsEmpty)
        {
            Emit(
                new EngineEffect.Inject(
                    transition.Events,
                    Epoch: null,
                    RequiredForeground: null,
                    IsRelease: true,
                    IsInternal: false
                )
                {
                    Holder = holder,
                }
            );
        }
    }

    /// <summary>Sends the events of a transition that presses something, as one batch.</summary>
    public void Press(LedgerTransition transition, HolderId holder, ExecutionOrigin origin)
    {
        State = State with { Keys = transition.Ledger };
        EmitPress(transition.Events, holder, origin);
    }

    /// <summary>
    /// Drops every queued step of <paramref name="holder"/> and releases what it holds (with the menu mask).
    /// Returns whether it held or was about to hold anything.
    /// </summary>
    public bool CancelHolder(HolderId holder)
    {
        var hadSteps = State.Outbox.Items.Any(step => step.Holder == holder);
        var held = State.Keys.Items.ContainsKey(holder);
        if (hadSteps)
        {
            State = State with
            {
                Outbox = new ValueList<QueuedStep>(
                    State.Outbox.Items.RemoveAll(step => step.Holder == holder)
                ),
            };
        }

        Release(State.Keys.Release(holder), holder);
        if (State.Macro is { } run && holder == MacroHolder(run))
        {
            State = State with { Macro = null };
        }

        return hadSteps || held;
    }

    /// <summary>
    /// «Release all» and the terminal events (SEG-003, SEG-007, INV-3): drops the outbox, cancels the macro, the
    /// repeating scroll and the armed confirmation, and releases everything in reverse order with the menu mask.
    /// Returns whether anything was held or about to be pressed.
    /// </summary>
    public bool ReleaseEverything(bool cancelPastes)
    {
        var wasBusy = !State.IsQuiet;
        State = State with
        {
            Outbox = [],
            OutboxDueTicks = null,
            Macro = null,
            Scroll = null,
            Armed = null,
            Sticky = StickyModifiers.StickyState.Empty,
        };
        if (cancelPastes)
        {
            State = State with
            {
                PendingExternal = State.PendingExternal.RemoveRange(
                    State
                        .PendingExternal.Values.Where(static p => p.Kind == PendingKind.Paste)
                        .Select(static p => p.Id)
                ),
            };
        }

        Release(State.Keys.ReleaseAll(), holder: null);

        // The releases the secure desktop refused go again too: «Release all» and the terminal events must let go of
        // everything Clícalo left down, not only of what its holders still hold (INV-3). An extra release is harmless.
        ResendBlockedReleases();
        return wasBusy;
    }

    /// <summary>
    /// Sends again the releases the secure desktop refused (INV-3), except those of a key or button a holder has
    /// pressed again since: that holder's own release lets go of it, and releasing it now would leave the ledger
    /// claiming a key that is up (INV-1).
    /// </summary>
    public void ResendBlockedReleases()
    {
        var blocked = State.BlockedReleases;
        if (blocked.IsEmpty)
        {
            return;
        }

        State = State with { BlockedReleases = [] };
        var held = State.Keys;
        var events = blocked
            .Items.Where(e =>
                e.Kind switch
                {
                    InjectedEventKind.KeyUp => !held.IsDown(e.Key),
                    InjectedEventKind.MouseUp => (held.HeldButtons & e.Button) == MouseButtons.None,
                    _ => e.IsRelease,
                }
            )
            .ToImmutableArray();
        if (events.Any(static e => e.Kind != InjectedEventKind.MenuMask))
        {
            Emit(
                new EngineEffect.Inject(
                    events,
                    Epoch: null,
                    RequiredForeground: null,
                    IsRelease: true,
                    IsInternal: false
                )
            );
        }
    }

    /// <summary>Appends steps to the outbox and runs it.</summary>
    public void Enqueue(IEnumerable<QueuedStep> steps)
    {
        State = State with
        {
            Outbox = new ValueList<QueuedStep>(State.Outbox.Items.AddRange(steps)),
        };
        RunOutbox();
    }

    /// <summary>
    /// Runs queued steps until one must wait (the pause between events, or the Shift burst limit of SEG-008). With a
    /// zero pause, consecutive steps of one holder go as a single atomic batch.
    /// </summary>
    public void RunOutbox()
    {
        if (State.OutboxDueTicks is { } due && due > Now)
        {
            return;
        }

        State = State with { OutboxDueTicks = null };
        var atomic = Config.InterEventDelay <= TimeSpan.Zero;
        var batch = new OutboxBatch();
        var burst = Timings.KeySafety.ShiftBurstLimit;
        var burstWindow = Ticks(burst.Window);
        while (!State.Outbox.IsEmpty)
        {
            var step = State.Outbox[0];
            if (!batch.IsEmpty && batch.Holder != step.Holder)
            {
                Flush(batch);
            }

            switch (step)
            {
                case QueuedStep.Press press:
                {
                    if (!CanPress(press.Origin))
                    {
                        // INV-6: presses planned for another foreground never go. What the holder already pressed
                        // stays held by its owner (INV-9) and goes up by its planned releases, its contact's end, its
                        // deadline or «Release all»; a macro stops.
                        Flush(batch);
                        if (State.Macro is { } run && press.Holder == MacroHolder(run))
                        {
                            CancelHolder(press.Holder);
                        }
                        else
                        {
                            DropPresses(press.Holder);
                        }

                        continue;
                    }

                    var countsAsShift =
                        InjectedKeyKinds.IsShift(press.Key) && !State.Keys.IsDown(press.Key);
                    if (countsAsShift)
                    {
                        var allowed = State.ShiftGuard.NextAllowed(Now, burst.Count, burstWindow);
                        if (allowed > Now)
                        {
                            Flush(batch);
                            State = State with { OutboxDueTicks = allowed };
                            return;
                        }
                    }

                    Pop();
                    var transition = State.Keys.Press(CurrentTemplate(press.Template), press.Key);
                    State = State with { Keys = transition.Ledger };
                    if (transition.Events.IsEmpty)
                    {
                        continue;
                    }

                    if (countsAsShift)
                    {
                        State = State with
                        {
                            ShiftGuard = State.ShiftGuard.Record(Now, burstWindow),
                        };
                    }

                    batch.Add(transition.Events, press.Holder, press.Origin);
                    break;
                }

                case QueuedStep.Lift lift:
                {
                    Pop();
                    var transition = State.Keys.Lift(lift.Owner, lift.Key);
                    State = State with { Keys = transition.Ledger };
                    if (transition.Events.IsEmpty)
                    {
                        continue;
                    }

                    batch.Add(transition.Events, lift.Owner, origin: null);
                    break;
                }

                case QueuedStep.Finish finish:
                    Flush(batch);
                    Pop();
                    Complete(finish);
                    continue;
            }

            if (!atomic)
            {
                Flush(batch);
                State = State with { OutboxDueTicks = Now + Ticks(Config.InterEventDelay) };
                return;
            }
        }

        Flush(batch);
    }

    /// <summary>Starts the next steps of the running macro until one has to wait (EJE-010).</summary>
    public void AdvanceMacro()
    {
        if (_advancingMacro)
        {
            return;
        }

        _advancingMacro = true;
        try
        {
            MacroPlanner.Advance(this);
        }
        finally
        {
            _advancingMacro = false;
        }
    }

    public static HolderId MacroHolder(MacroRun run) => HolderId.ForMacro(run.Id.Value);

    /// <summary>
    /// The timers the state needs, compared with the ones <paramref name="before"/> needed: a single timer per key
    /// (deadline, outbox, confirmation, macro wait, scroll repeat).
    /// </summary>
    public void UpdateTimers(EngineState before)
    {
        var old = TimerPlan(before);
        var next = TimerPlan(State);
        foreach (var (key, due) in next)
        {
            if (!old.TryGetValue(key, out var oldDue) || oldDue != due)
            {
                Emit(new EngineEffect.Schedule(key, due));
            }
        }

        foreach (var key in old.Keys)
        {
            if (!next.ContainsKey(key))
            {
                Emit(new EngineEffect.CancelTimer(key));
            }
        }
    }

    private Dictionary<TimerKey, long> TimerPlan(EngineState state)
    {
        var plan = new Dictionary<TimerKey, long>();
        if (state.Keys.NextDeadline() is { } deadline)
        {
            plan[EngineTimers.Deadline] = deadline;
        }

        if (!state.Outbox.IsEmpty)
        {
            plan[EngineTimers.Outbox] = state.OutboxDueTicks ?? Now;
        }

        if (state.Armed is { } armed)
        {
            plan[EngineTimers.Confirm] = armed.UntilTicks;
        }

        if (state.Macro?.WaitingUntilTicks is { } wait)
        {
            plan[EngineTimers.Macro] = wait;
        }

        if (state.Scroll is { } scroll)
        {
            plan[EngineTimers.Scroll] = scroll.NextTicks;
        }

        return plan;
    }

    private void Pop() =>
        State = State with { Outbox = new ValueList<QueuedStep>(State.Outbox.Items.RemoveAt(0)) };

    /// <summary>Drops the queued presses and the completion of a holder, keeping its planned releases.</summary>
    private void DropPresses(HolderId holder)
    {
        State = State with
        {
            Outbox = new ValueList<QueuedStep>(
                State.Outbox.Items.RemoveAll(step =>
                    step.Holder == holder && step is QueuedStep.Press or QueuedStep.Finish
                )
            ),
        };
    }

    /// <summary>
    /// A template whose deadline follows the global limit gets it from the settings in force when its first key is
    /// sent, so a limit changed while the steps waited applies (SEG-004).
    /// </summary>
    private PressedItem CurrentTemplate(PressedItem template) =>
        template.InheritsGlobalLimit
            ? template with
            {
                DeadlineTicks = Config.MaxHold is { } max ? template.SinceTicks + Ticks(max) : null,
            }
            : template;

    private void Complete(QueuedStep.Finish finish)
    {
        var completion = finish.Completion;
        if (completion.Notice is { } notice)
        {
            Notice(notice);
        }

        if (completion.CountsUsage)
        {
            CountUsage(completion.Origin, completion.Repeatable);
        }

        if (completion.ContinueMacro is { } run && State.Macro?.Id == run)
        {
            AdvanceMacro();
        }
    }

    /// <summary>
    /// After an action ran (EJE-012): it counts for Frequents (FRE-002), becomes the last action for Repeat unless it
    /// is a Hold or a Toggle (AVI-004), and the soft sound plays when it is on.
    /// </summary>
    public void CountUsage(ExecutionOrigin origin, bool repeatable = true)
    {
        // PRB-006: a try in the editor is not the user's use.
        if (origin.Trial)
        {
            return;
        }

        Emit(new EngineEffect.CountUsage(origin.Shortcut, origin.At));
        if (repeatable)
        {
            Emit(new EngineEffect.SetLastAction(origin.Shortcut));
        }

        if (Config.FeedbackSound)
        {
            Emit(new EngineEffect.PlayFeedbackSound());
        }
    }

    /// <summary>The name of <paramref name="shortcut"/> in the interface language, for the notices.</summary>
    public string NameOf(Shortcut shortcut) =>
        shortcut.Name.Get(
            Config.InterfaceLanguage,
            Config.InterfaceLanguage == LangCode.Es ? LangCode.En : LangCode.Es
        );

    /// <summary>The app in front as the notices name it: its process without «.exe» (EJE-003).</summary>
    public string AppName()
    {
        var process = State.Foreground?.Process.Value ?? string.Empty;
        return process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? process[..^4]
            : process;
    }

    /// <summary>A combination written with the key labels of the interface language («Ctrl + S»).</summary>
    public string KeysText(ValueList<Keys.KeyStroke> strokes) =>
        Keys.KeyChordFormatter.Format(
            Keys.KeyChord.Create(strokes),
            Config.KeyLabels,
            Keys.KeyLabelStyle.Full,
            Config.InterfaceLanguage,
            LangCode.En
        );

    private void Flush(OutboxBatch batch)
    {
        if (batch.IsEmpty)
        {
            return;
        }

        if (batch.Origin is { } origin)
        {
            EmitPress(batch.Events.ToImmutable(), batch.Holder, origin);
        }
        else
        {
            Emit(
                new EngineEffect.Inject(
                    batch.Events.ToImmutable(),
                    Epoch: null,
                    RequiredForeground: null,
                    IsRelease: true,
                    IsInternal: false
                )
                {
                    Holder = batch.Holder,
                }
            );
        }

        batch.Clear();
    }

    private void EmitPress(
        ImmutableArray<InjectedEvent> events,
        HolderId holder,
        ExecutionOrigin origin
    )
    {
        if (events.IsEmpty)
        {
            return;
        }

        var effect = new EffectId(NextSequence());
        Remember(
            effect,
            new PressedBatch(
                holder,
                [
                    .. events
                        .Where(static e =>
                            e.Kind is InjectedEventKind.KeyDown or InjectedEventKind.KeyUp
                        )
                        .Select(static e => e.Key)
                        .Distinct(),
                ],
                events
                    .Where(static e =>
                        e.Kind is InjectedEventKind.MouseDown or InjectedEventKind.MouseUp
                    )
                    .Aggregate(MouseButtons.None, static (all, e) => all | e.Button)
            )
        );
        Emit(
            new EngineEffect.Inject(
                events,
                origin.Epoch,
                origin.RequiredForeground,
                IsRelease: false,
                IsInternal: false
            )
            {
                Effect = effect,
                Holder = holder,
            }
        );
    }

    private void Remember(EffectId effect, PressedBatch batch)
    {
        var remembered = State.PressEffects.SetItem(effect, batch);
        if (remembered.Count > EngineState.PressMemory)
        {
            remembered = remembered.RemoveRange(
                remembered
                    .Keys.OrderBy(static e => e.Value)
                    .Take(remembered.Count - EngineState.PressMemory)
            );
        }

        State = State with { PressEffects = remembered };
    }

    /// <summary>Events of consecutive steps of one holder, sent together when there is no pause between events.</summary>
    private sealed class OutboxBatch
    {
        public ImmutableArray<InjectedEvent>.Builder Events { get; } =
            ImmutableArray.CreateBuilder<InjectedEvent>();

        public HolderId Holder { get; private set; }

        public ExecutionOrigin? Origin { get; private set; }

        public bool IsEmpty => Events.Count == 0;

        public void Add(
            ImmutableArray<InjectedEvent> events,
            HolderId holder,
            ExecutionOrigin? origin
        )
        {
            Holder = holder;
            Origin ??= origin;
            Events.AddRange(events);
        }

        public void Clear()
        {
            Events.Clear();
            Origin = null;
        }
    }
}
