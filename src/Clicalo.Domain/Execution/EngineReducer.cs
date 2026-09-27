using System.Collections.Immutable;
using Clicalo.Domain.Execution.Internal;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Execution;

/// <summary>
/// The functional core of the engine (blueprint §7.3, ADR-0004): a pure function from state and event to a new state
/// and effects, with <see cref="ActivationPolicy"/> and one planner per <see cref="Library.ActionKind"/> inside.
/// INV-1 to INV-12 (§7.5) are properties of this function, checked on every step with CsCheck.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Presses and planned releases leave through the outbox (<see cref="QueuedStep"/>), one event per
/// <see cref="EngineConfig.InterEventDelay"/>; the logical ledger changes only when a step is sent, so what it holds is
/// what was sent (INV-1). Safety releases (end of contact, deadline, «Release all», terminal events) leave at once and
/// carry no epoch: nothing filters them (INV-8).</item>
/// <item>Every press is checked against the current foreground epoch, window and elevation, and against test mode and
/// pause, when it is sent, not only when it was planned (INV-6, INV-7).</item>
/// <item>Every release uses the physical key its press recorded, so the mode, vk and scan match (INV-12).</item>
/// </list>
/// </remarks>
public static class EngineReducer
{
    /// <summary>One step of the engine.</summary>
    /// <param name="state">The current state.</param>
    /// <param name="engineEvent">The event.</param>
    /// <param name="config">The settings the engine obeys.</param>
    /// <param name="nowTicks">Now, in <see cref="TimeProvider"/> ticks.</param>
    public static EngineTransition Reduce(
        EngineState state,
        EngineEvent engineEvent,
        EngineConfig config,
        long nowTicks
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(engineEvent);
        ArgumentNullException.ThrowIfNull(config);

        var step = new EngineStep(state, config, nowTicks);
        switch (engineEvent)
        {
            case EngineEvent.Activation activation:
                Activate(step, activation);
                break;
            case EngineEvent.ContactEnded ended:
                ContactEnded(step, ended.ContactId);
                break;
            case EngineEvent.TimerFired timer:
                TimerFired(step, timer.Key);
                break;
            case EngineEvent.ForegroundChanged changed:
                ForegroundChanged(step, changed.Info, changed.IsUserSwitch);
                break;
            case EngineEvent.LayoutChanged layout when step.State.Foreground is { } foreground:
                step.State = step.State with
                {
                    Foreground = foreground with { Layout = layout.Layout },
                };
                break;
            case EngineEvent.Terminal terminal:
                Terminal(step, terminal.Reason);
                break;
            case EngineEvent.ReleaseAll releaseAll:
                ReleaseAll(step, releaseAll.Reason);
                break;
            case EngineEvent.InjectFailed failed:
                InjectFailed(step, failed.Effect);
                break;
            case EngineEvent.ReleasesBlocked blocked:
                step.State = step.State with
                {
                    BlockedReleases = [.. step.State.BlockedReleases, .. blocked.Events],
                };
                break;
            case EngineEvent.SessionResumed:
                SessionResumed(step);
                break;
            case EngineEvent.StickyTapped tapped when !step.State.Paused && !step.State.TestMode:
                StickyPlanner.Tap(step, tapped.Modifier);
                break;
            case EngineEvent.ClearSticky:
                StickyPlanner.Clear(step);
                break;
            case EngineEvent.ClipboardReady ready:
                TextPlanner.ClipboardReady(step, ready.Effect);
                break;
            case EngineEvent.ConfigChanged changed:
                step.State = step.State with
                {
                    Keys = step.State.Keys.WithGlobalLimit(
                        changed.Config.MaxHold,
                        changed.Config.TimestampFrequency
                    ),
                    Scroll = step.State.Scroll is { } scroll
                        ? scroll with
                        {
                            DeadlineTicks = changed.Config.MaxHold is { } max
                                ? scroll.SinceTicks + step.Ticks(max)
                                : null,
                        }
                        : null,
                };
                break;
            case EngineEvent.SetTestMode testMode:
                if (testMode.On)
                {
                    step.ReleaseEverything(cancelPastes: true);
                }

                step.State = step.State with { TestMode = testMode.On };
                break;
            case EngineEvent.SetPaused paused:
                if (paused.On)
                {
                    step.ReleaseEverything(cancelPastes: true);
                }

                step.State = step.State with { Paused = paused.On };
                break;
            case EngineEvent.LaunchCompleted completed:
                LaunchPlanner.Completed(step, completed.Effect);
                break;
            case EngineEvent.LaunchFailed failed:
                LaunchPlanner.Failed(step, failed.Effect, failed.Failure);
                break;
            case EngineEvent.SystemCommandCompleted completed:
                LaunchPlanner.SystemCompleted(step, completed.Effect);
                break;
        }

        step.UpdateTimers(state);
        step.State = step.State with { Version = state.Version + 1 };
        return new EngineTransition(step.State, step.Effects);
    }

    private static void Activate(EngineStep step, EngineEvent.Activation activation)
    {
        if (step.State.Paused)
        {
            return;
        }

        var shortcut = activation.Shortcut;
        var request = activation.Request;
        var holdLike = IsHoldLike(shortcut.Action) && request.Phase != ActivationPhase.Invoke;
        if (
            (request.Phase == ActivationPhase.ContactStarted && !holdLike)
            || (request.Phase == ActivationPhase.ContactEnded && holdLike)
        )
        {
            return;
        }

        var filter = step.State.Filters.TryGetValue(shortcut.Id, out var stored) ? stored : default;
        var context = new ActivationContext(
            request,
            shortcut,
            activation.OriginProfile,
            activation.Injection,
            activation.LastExternalPointer,
            activation.EditMode,
            step.State.TestMode,
            step.State.Foreground?.Elevation ?? ElevationState.Unknown,
            step.State.Armed,
            filter,
            step.Config.Touch,
            request.At
        );
        var outcome = ActivationPolicy.Decide(context);
        step.State = step.State with
        {
            Filters = step.State.Filters.SetItem(shortcut.Id, outcome.NextFilter),
        };
        var origin = new ExecutionOrigin(
            shortcut.Id,
            activation.Epoch,
            activation.RequiredForeground,
            activation.Injection,
            activation.LastExternalPointer,
            request.At
        );

        // A latched Toggle (or drag, or invoked Hold) and a running macro stop on the next accepted tap, whatever the
        // elevation, the completeness or the confirmation say: stopping only releases (INV-8).
        if (
            outcome.Decision
            is ActivationDecision.Execute
                or ActivationDecision.Armed
                or ActivationDecision.BlockedElevated
                or ActivationDecision.Refused
        )
        {
            var toggle = HolderId.ForToggle(shortcut.Id);
            if (KeyPlanner.IsHeld(step, toggle))
            {
                Disarm(step);
                KeyPlanner.Unlatch(step, toggle);
                return;
            }

            if (step.State.Macro is { } running && running.Shortcut == shortcut.Id)
            {
                Disarm(step);
                MacroPlanner.Cancel(step, running);
                return;
            }
        }

        switch (outcome.Decision)
        {
            case ActivationDecision.BlockedElevated:
                step.Notice(
                    EngineNotices.ElevatedRefused(
                        step.State.Foreground?.Process.Value ?? string.Empty
                    ),
                    NoticeUrgency.Assertive
                );
                break;
            case ActivationDecision.Armed armed:
                step.State = step.State with
                {
                    Armed = new ArmedConfirmation(armed.Shortcut, armed.Until)
                    {
                        UntilTicks =
                            step.Now + step.Ticks(Timings.Confirmation.ExecuteConfirmWindow),
                    },
                };
                step.Notice(EngineNotices.ConfirmArmed, NoticeUrgency.Assertive);
                break;
            case ActivationDecision.Refused refused:
                step.Notice(
                    refused.Reason == RefusalReason.BlockedCombo
                        ? EngineNotices.Blocked
                        : EngineNotices.Incomplete,
                    NoticeUrgency.Assertive
                );
                break;
            case ActivationDecision.Execute execute:
                Disarm(step);
                Execute(
                    step,
                    activation,
                    origin with
                    {
                        Injection = execute.Injection,
                    },
                    execute.Shortcut
                );
                break;
        }
    }

    private static void Execute(
        EngineStep step,
        EngineEvent.Activation activation,
        ExecutionOrigin origin,
        Shortcut shortcut
    )
    {
        var contact = activation.Request.ContactId;
        var invoked = activation.Request.Phase == ActivationPhase.Invoke || contact is null;
        switch (shortcut.Action)
        {
            case TapAction tap:
                KeyPlanner.Tap(step, origin, ChordFor(tap, step.Config));
                break;
            case HoldAction hold when invoked:
                KeyPlanner.Toggle(step, origin, shortcut, hold.Chord, HoldOrigin.Invoke);
                break;
            case HoldAction hold:
                KeyPlanner.HoldWithContact(step, origin, shortcut, hold.Chord, contact!.Value);
                break;
            case ToggleAction toggle:
                KeyPlanner.Toggle(step, origin, shortcut, toggle.Chord, HoldOrigin.Toggle);
                break;
            case TextAction text:
                TextPlanner.Plan(step, origin, text);
                break;
            case MouseAction mouse when MousePlanner.IsScroll(mouse.Op) && !invoked:
                MousePlanner.StartScroll(step, origin, mouse, contact!.Value);
                break;
            case MouseAction mouse:
                MousePlanner.Plan(step, origin, shortcut, mouse);
                break;
            case MacroAction macro:
                MacroPlanner.Start(step, origin, macro);
                break;
            default:
                LaunchPlanner.Plan(step, origin, shortcut.Action);
                break;
        }
    }

    private static KeyChord ChordFor(TapAction tap, EngineConfig config) =>
        config.AppsLanguage is { } language
        && tap.Variants.Items.FirstOrDefault(v => v.AppsLanguage == language) is { } variant
            ? variant.Chord
            : tap.Chord;

    private static bool IsHoldLike(ShortcutAction action) =>
        action is HoldAction || (action is MouseAction mouse && MousePlanner.IsScroll(mouse.Op));

    private static void Disarm(EngineStep step)
    {
        if (step.State.Armed is not null)
        {
            step.State = step.State with { Armed = null };
        }
    }

    private static void ContactEnded(EngineStep step, int contactId)
    {
        if (step.CancelHolder(HolderId.ForContact(contactId)))
        {
            step.Notice(EngineNotices.Released);
        }

        if (step.State.Scroll?.ContactId == contactId)
        {
            step.State = step.State with { Scroll = null };
        }
    }

    private static void TimerFired(EngineStep step, TimerKey key)
    {
        if (key == EngineTimers.Outbox)
        {
            step.RunOutbox();
        }
        else if (key == EngineTimers.Deadline)
        {
            Expire(step);
        }
        else if (key == EngineTimers.Confirm)
        {
            if (step.State.Armed is { } armed && armed.UntilTicks <= step.Now)
            {
                step.State = step.State with { Armed = null };
            }
        }
        else if (key == EngineTimers.Macro)
        {
            MacroPlanner.WaitTimer(step);
        }
        else if (key == EngineTimers.Scroll)
        {
            MousePlanner.ScrollTimer(step);
        }
    }

    private static void Expire(EngineStep step)
    {
        foreach (var holder in step.State.Keys.ExpiredAt(step.Now))
        {
            var item = step.State.Keys.Items[holder];
            step.CancelHolder(holder);
            StickyPlanner.Expired(step, holder);
            if (item.Origin != HoldOrigin.Tap && item.DeadlineTicks is { } deadline)
            {
                var seconds =
                    (deadline - item.SinceTicks) / Math.Max(1, step.Config.TimestampFrequency);
                step.Notice(EngineNotices.ReleasedAutomatically(seconds), NoticeUrgency.Assertive);
            }
        }
    }

    private static void ForegroundChanged(EngineStep step, ForegroundInfo info, bool isUserSwitch)
    {
        var before = step.State.Foreground;
        step.State = step.State with { Foreground = info };
        if (isUserSwitch)
        {
            // A real switch cancels the macro and anything queued for the old foreground (EJE-010, §7.6).
            if (step.State.Macro is { } run)
            {
                MacroPlanner.Cancel(step, run);
            }

            step.State = step.State with { Scroll = null };
            foreach (
                var holder in step
                    .State.Outbox.Items.Select(static s => s.Holder)
                    .Distinct()
                    .ToList()
            )
            {
                step.CancelHolder(holder);
            }

            if (step.Config.ReleaseOnAppSwitch && !step.State.Keys.IsEmpty)
            {
                step.ReleaseEverything(cancelPastes: true);
                step.Notice(EngineNotices.ReleasedOnSwitch, NoticeUrgency.Assertive);
                return;
            }
        }

        if (
            info.Elevation == ElevationState.TargetElevated
            && before?.Elevation != ElevationState.TargetElevated
            && !step.State.IsQuiet
        )
        {
            ReleaseAll(step, ReleaseReason.TargetElevated);
        }
    }

    private static void Terminal(EngineStep step, TerminalReason reason)
    {
        var wasBusy = step.ReleaseEverything(cancelPastes: true);
        switch (reason)
        {
            case TerminalReason.Pause:
                step.State = step.State with { Paused = true };
                break;
            case TerminalReason.EngineFault:
                step.State = EngineState.Empty with
                {
                    Foreground = step.State.Foreground,
                    Filters = step.State.Filters,
                    Sequence = step.State.Sequence,
                    BlockedReleases = step.State.BlockedReleases,
                };
                break;
        }

        if (
            wasBusy
            && reason
                is TerminalReason.Lock
                    or TerminalReason.Suspend
                    or TerminalReason.SessionEnd
                    or TerminalReason.Hide
                    or TerminalReason.Pause
                    or TerminalReason.ViewChange
                    or TerminalReason.EngineFault
        )
        {
            step.Notice(
                reason switch
                {
                    TerminalReason.Lock or TerminalReason.Suspend => EngineNotices.ReleasedOnLock,
                    TerminalReason.EngineFault => EngineNotices.Fault,
                    _ => EngineNotices.ReleasedAll,
                },
                NoticeUrgency.Assertive
            );
        }
    }

    private static void ReleaseAll(EngineStep step, ReleaseReason reason)
    {
        var wasBusy = step.ReleaseEverything(cancelPastes: true);
        if (reason == ReleaseReason.AppSwitch)
        {
            if (wasBusy)
            {
                step.Notice(EngineNotices.ReleasedOnSwitch, NoticeUrgency.Assertive);
            }
        }
        else if (reason == ReleaseReason.User || wasBusy)
        {
            step.Notice(EngineNotices.ReleasedAll, NoticeUrgency.Assertive);
        }
    }

    private static void InjectFailed(EngineStep step, EffectId effect)
    {
        if (!step.State.PressEffects.TryGetValue(effect, out var batch))
        {
            return;
        }

        // INV-5: SendInput took only part of the batch. Release the holder, and send an up for every key or button the
        // batch pressed or released that the ledger no longer counts (a Tap lifts in the same batch), in case it stayed
        // down. A release of a key that is up changes nothing.
        step.State = step.State with
        {
            PressEffects = step.State.PressEffects.Remove(effect),
        };
        var before = step.State.Keys;
        step.CancelHolder(batch.Holder);

        // A holder that joined a key of the failed batch afterwards counts on a press that may never have happened:
        // release it too, so the ledger never claims a key that is not down (INV-1).
        foreach (
            var joined in batch
                .Keys.Where(step.State.Keys.IsDown)
                .SelectMany(key => step.State.Keys.Holders[key])
                .Distinct()
                .ToList()
        )
        {
            step.CancelHolder(joined);
        }

        var after = step.State.Keys;
        var compensation = ImmutableArray.CreateBuilder<InjectedEvent>();
        for (var i = batch.Keys.Length - 1; i >= 0; i--)
        {
            var key = batch.Keys[i];
            if (!before.IsDown(key) && !after.IsDown(key))
            {
                if (InjectedKeyKinds.IsAltOrWin(key))
                {
                    compensation.Add(InjectedEvent.MenuMask(key.Mode));
                }

                compensation.Add(InjectedEvent.KeyUp(key));
            }
        }

        var lostButtons = batch.Buttons & ~before.HeldButtons & ~after.HeldButtons;
        foreach (var button in Enum.GetValues<MouseButtons>())
        {
            if (button != MouseButtons.None && (lostButtons & button) != MouseButtons.None)
            {
                compensation.Add(InjectedEvent.MouseUp(button));
            }
        }

        if (compensation.Count > 0)
        {
            step.Emit(
                new EngineEffect.Inject(
                    compensation.ToImmutable(),
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

        if (step.State.Foreground is { Elevation: ElevationState.Unknown } foreground)
        {
            // EC-PER-03: the elevation could not be read, the send failed: say why it may have failed (EJE-013).
            step.Notice(EngineNotices.Elevated(foreground.Process.Value), NoticeUrgency.Assertive);
        }
    }

    private static void SessionResumed(EngineStep step)
    {
        var blocked = step.State.BlockedReleases;
        if (blocked.IsEmpty)
        {
            return;
        }

        step.State = step.State with { BlockedReleases = [] };
        step.Emit(
            new EngineEffect.Inject(
                blocked.Items,
                Epoch: null,
                RequiredForeground: null,
                IsRelease: true,
                IsInternal: false
            )
        );
    }
}
