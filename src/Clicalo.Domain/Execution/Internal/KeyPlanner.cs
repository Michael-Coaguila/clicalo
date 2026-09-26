using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// The planners of the key actions (blueprint §7.3): Tap (EJE-003), Hold with a contact (EJE-004, EJE-006) and
/// Toggle, including a Hold invoked without contact (EJE-005, EJE-007). They only queue steps and change the state;
/// the outbox sends.
/// </summary>
internal static class KeyPlanner
{
    /// <summary>
    /// Tap: press in the saved order, release in reverse order, one event per pause (EJE-003). Its keys are held by a
    /// Tap holder while it runs, so a key another holder keeps is neither pressed again nor released under it.
    /// </summary>
    public static void Tap(EngineStep step, ExecutionOrigin origin, KeyChord chord)
    {
        // FIJ-006 (a): the active sticky modifiers go first; the ones at «once» are used up.
        if (Tap(step, origin, StickyPlanner.Compose(step, chord.Strokes), countsUsage: true))
        {
            StickyPlanner.Consume(step);
        }
    }

    /// <summary>A Tap of exactly <paramref name="strokes"/>; <see langword="false"/> when nothing was planned.</summary>
    public static bool Tap(
        EngineStep step,
        ExecutionOrigin origin,
        ValueList<KeyStroke> strokes,
        bool countsUsage
    )
    {
        if (!KeyResolver.TryResolve(strokes, origin.Injection, step.Layout, out var keys))
        {
            step.Notice(EngineNotices.Incomplete, NoticeUrgency.Assertive);
            return false;
        }

        if (keys.IsEmpty)
        {
            return false;
        }

        var holder = HolderId.ForTap(step.NextSequence());
        var (deadline, inherits) = step.GlobalDeadline();
        var template = Template(
            step,
            holder,
            HoldOrigin.Tap,
            origin,
            contact: null,
            deadline,
            inherits
        );
        step.Enqueue(
            PressThenLift(template, keys, origin)
                .Append(
                    new QueuedStep.Finish(
                        holder,
                        new StepCompletion(origin, Notice: null, countsUsage, ContinueMacro: null)
                    )
                )
        );
        return true;
    }

    /// <summary>A Hold under a contact: presses when the contact starts, releases when it ends (EJE-004, INV-9).</summary>
    public static void HoldWithContact(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        KeyChord chord,
        int contactId
    )
    {
        var holder = HolderId.ForContact(contactId);
        if (IsHeld(step, holder))
        {
            return;
        }

        Latch(
            step,
            origin,
            shortcut,
            chord,
            holder,
            HoldOrigin.Contact,
            contactId,
            EngineNotices.Holding
        );
    }

    /// <summary>
    /// A Toggle, or a Hold invoked without contact (EJE-005): the first execution presses, the next one releases
    /// (EJE-007).
    /// </summary>
    public static void Toggle(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        KeyChord chord,
        HoldOrigin kind
    )
    {
        var holder = HolderId.ForToggle(shortcut.Id);
        if (IsHeld(step, holder))
        {
            Unlatch(step, holder);
            return;
        }

        Latch(step, origin, shortcut, chord, holder, kind, contact: null, EngineNotices.Latched);
    }

    /// <summary>Releases a latched Toggle (EJE-007), with the menu mask.</summary>
    public static void Unlatch(EngineStep step, HolderId holder)
    {
        if (step.CancelHolder(holder))
        {
            step.Notice(EngineNotices.Unlatched);
        }
    }

    /// <summary>Whether <paramref name="holder"/> holds something or has steps queued.</summary>
    public static bool IsHeld(EngineStep step, HolderId holder) =>
        step.State.Keys.Items.ContainsKey(holder)
        || step.State.Outbox.Items.Any(queued => queued.Holder == holder);

    /// <summary>The item template of a holder that starts now.</summary>
    public static PressedItem Template(
        EngineStep step,
        HolderId holder,
        HoldOrigin kind,
        ExecutionOrigin origin,
        int? contact,
        long? deadline,
        bool inheritsGlobal
    ) =>
        new(holder, kind, origin.Shortcut, contact, [], MouseButtons.None, step.Now, deadline)
        {
            InheritsGlobalLimit = inheritsGlobal,
        };

    /// <summary>The press steps of <paramref name="keys"/> followed by their releases in reverse order.</summary>
    public static IEnumerable<QueuedStep> PressThenLift(
        PressedItem template,
        ImmutableArray<InjectedKey> keys,
        ExecutionOrigin origin
    )
    {
        foreach (var key in keys)
        {
            yield return new QueuedStep.Press(template, key, origin);
        }

        for (var i = keys.Length - 1; i >= 0; i--)
        {
            yield return new QueuedStep.Lift(template.Holder, keys[i]);
        }
    }

    private static void Latch(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        KeyChord chord,
        HolderId holder,
        HoldOrigin kind,
        int? contact,
        Message notice
    )
    {
        // FIJ-006 (c): the active sticky modifiers are added while the button is held.
        var strokes = StickyPlanner.Compose(step, chord.Strokes);
        if (!KeyResolver.TryResolve(strokes, origin.Injection, step.Layout, out var keys))
        {
            step.Notice(EngineNotices.Incomplete, NoticeUrgency.Assertive);
            return;
        }

        if (keys.IsEmpty)
        {
            return;
        }

        StickyPlanner.Consume(step);
        var (deadline, inherits) = step.Deadline(shortcut.Options.MaxHold);
        var template = Template(step, holder, kind, origin, contact, deadline, inherits);
        step.Enqueue(
            keys.Select(key => (QueuedStep)new QueuedStep.Press(template, key, origin))
                .Append(
                    new QueuedStep.Finish(
                        holder,
                        new StepCompletion(origin, notice, CountsUsage: true, ContinueMacro: null)
                    )
                )
        );
    }
}
