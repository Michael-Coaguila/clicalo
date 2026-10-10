using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// The mouse actions (EJE-009, blueprint §7.11): they act at the last pointer position outside Clícalo. Clicks go as
/// one effect (move, press and release together); a drag is a Toggle of the left button held by the ledger; a scroll
/// repeats one wheel step per <c>Timings.Mouse.ScrollRepeat*</c> while its contact lasts, faster and faster.
/// </summary>
internal static class MousePlanner
{
    /// <summary>Whether <paramref name="op"/> repeats while held (the four scroll directions).</summary>
    public static bool IsScroll(MouseOp op) => MouseOps.RepeatsWhileHeld(op);

    /// <summary>A click, a drag toggle or one scroll step started by a tap or an invocation.</summary>
    public static void Plan(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        MouseAction mouse
    )
    {
        if (mouse.Op == MouseOp.Drag)
        {
            Drag(step, origin, shortcut);
            return;
        }

        if (!step.CanPress(origin))
        {
            return;
        }

        // FIJ-006 (b): the active sticky modifiers are held around the click (Ctrl+click), then used up.
        var modifiers = StickyKeys(step, origin);
        HolderId? holder = null;
        if (!modifiers.IsEmpty)
        {
            holder = HolderId.ForTap(step.NextSequence());
            var (deadline, inherits) = step.GlobalDeadline();
            var item = KeyPlanner.Template(
                step,
                holder.Value,
                HoldOrigin.Tap,
                origin,
                null,
                deadline,
                inherits
            ) with
            {
                Keys = new ValueList<InjectedKey>(modifiers),
            };
            step.Press(step.State.Keys.Acquire(item), holder.Value, origin);
        }

        step.Emit(
            new EngineEffect.MouseAction(
                mouse.Op,
                mouse.Speed,
                origin.ExternalPointer,
                origin.Epoch
            )
        );
        if (holder is { } pressed)
        {
            step.Release(step.State.Keys.Release(pressed), pressed);
            StickyPlanner.Consume(step);
        }

        step.Notice(L.MouseRan(name: step.NameOf(shortcut)));
        step.CountUsage(origin);
    }

    /// <summary>The physical keys of the active sticky modifiers (empty when none is active).</summary>
    private static ImmutableArray<InjectedKey> StickyKeys(
        EngineStep step,
        ExecutionOrigin origin
    ) =>
        KeyResolver.TryResolve(
            StickyPlanner.Active(step),
            origin.Injection,
            step.Layout,
            out var keys
        )
            ? keys
            : [];

    /// <summary>A scroll under a contact: one step now, then one per interval until the contact ends (EJE-009).</summary>
    public static void StartScroll(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        MouseAction mouse,
        int contactId
    )
    {
        if (!step.CanPress(origin))
        {
            return;
        }

        // One repeating scroll at a time, and one item per contact: the finger that holds keeps what it holds until it
        // lifts (INV-9), so a second scroll waits for the first to end.
        var holder = HolderId.ForContact(contactId);
        if (step.State.Scroll is not null || KeyPlanner.IsHeld(step, holder))
        {
            return;
        }

        step.Emit(
            new EngineEffect.MouseAction(
                mouse.Op,
                mouse.Speed,
                origin.ExternalPointer,
                origin.Epoch
            )
        );
        var (deadline, inherits) = step.GlobalDeadline();

        // SEG-001: the held scroll is in the ledger like any Hold, with no key and no button, so the panic strip shows
        // it and «Release all», the limit and every terminal event end it.
        var item = KeyPlanner.Template(
            step,
            holder,
            HoldOrigin.Dock,
            origin,
            contactId,
            deadline,
            inherits
        );
        step.State = step.State with
        {
            Keys = step.State.Keys.Acquire(item).Ledger,
            Scroll = new ScrollRepeat(
                contactId,
                mouse.Op,
                mouse.Speed,
                origin,
                step.Now,
                Steps: 1,
                step.Now + step.Ticks(Interval(mouse.Speed, 1)),
                deadline
            ),
        };
        step.Notice(L.MouseRan(name: step.NameOf(shortcut)));
        step.CountUsage(origin);
    }

    /// <summary>The scroll timer fired: one more step, or the end of the repeat.</summary>
    public static void ScrollTimer(EngineStep step)
    {
        if (step.State.Scroll is not { } scroll || scroll.NextTicks > step.Now)
        {
            return;
        }

        if (scroll.DeadlineTicks is { } deadline && deadline <= step.Now)
        {
            // SEG-004: the global limit ends it and says so, like any other held item.
            var seconds =
                (deadline - scroll.SinceTicks) / Math.Max(1, step.Config.TimestampFrequency);
            StopScroll(step);
            step.Notice(EngineNotices.ReleasedAutomatically(seconds), NoticeUrgency.Assertive);
            return;
        }

        if (!step.CanPress(scroll.Origin))
        {
            StopScroll(step);
            return;
        }

        step.Emit(
            new EngineEffect.MouseAction(
                scroll.Op,
                scroll.Speed,
                scroll.Origin.ExternalPointer,
                scroll.Origin.Epoch
            )
        );
        var steps = scroll.Steps + 1;
        step.State = step.State with
        {
            Scroll = scroll with
            {
                Steps = steps,
                NextTicks = step.Now + step.Ticks(Interval(scroll.Speed, steps)),
            },
        };
    }

    /// <summary>Ends the repeating scroll and takes its item out of the ledger (SEG-001); it sends nothing.</summary>
    public static void StopScroll(EngineStep step)
    {
        if (step.State.Scroll is not { } scroll)
        {
            return;
        }

        step.State = step.State with { Scroll = null };
        var holder = HolderId.ForContact(scroll.ContactId);
        if (
            step.State.Keys.Items.TryGetValue(holder, out var item)
            && item.Origin == HoldOrigin.Dock
        )
        {
            step.Release(step.State.Keys.Release(holder), holder);
        }
    }

    /// <summary>
    /// The scroll ends with its ledger item: its deadline or a cancelled holder took the item away (SEG-001, SEG-004).
    /// </summary>
    public static void SyncScroll(EngineStep step)
    {
        if (
            step.State.Scroll is { } scroll
            && !(
                step.State.Keys.Items.TryGetValue(
                    HolderId.ForContact(scroll.ContactId),
                    out var item
                )
                && item.Origin == HoldOrigin.Dock
            )
        )
        {
            step.State = step.State with { Scroll = null };
        }
    }

    /// <summary>
    /// The repeat interval after <paramref name="steps"/> steps: the interval of the speed, a quarter shorter every
    /// <c>Timings.Engine.ScrollAccelerationEvery</c> steps, never below <c>Timings.Engine.ScrollRepeatFloor</c>.
    /// </summary>
    public static TimeSpan Interval(ScrollSpeed speed, int steps)
    {
        var interval = speed switch
        {
            ScrollSpeed.Slow => Timings.Mouse.ScrollRepeatSlow,
            ScrollSpeed.Fast => Timings.Mouse.ScrollRepeatFast,
            _ => Timings.Mouse.ScrollRepeatNormal,
        };
        var accelerations = steps / Math.Max(1, Timings.Engine.ScrollAccelerationEvery);
        for (var i = 0; i < accelerations && interval > Timings.Engine.ScrollRepeatFloor; i++)
        {
            interval = TimeSpan.FromTicks(interval.Ticks * 3 / 4);
        }

        return interval < Timings.Engine.ScrollRepeatFloor
            ? Timings.Engine.ScrollRepeatFloor
            : interval;
    }

    private static void Drag(EngineStep step, ExecutionOrigin origin, Shortcut shortcut)
    {
        var holder = HolderId.ForToggle(shortcut.Id);
        if (step.State.Keys.Items.ContainsKey(holder))
        {
            KeyPlanner.Unlatch(step, holder);
            return;
        }

        if (!step.CanPress(origin))
        {
            return;
        }

        // Move to the point first (the host's mouse action for a drag only moves), then hold the left button.
        step.Emit(
            new EngineEffect.MouseAction(
                MouseOp.Drag,
                ScrollSpeed.Normal,
                origin.ExternalPointer,
                origin.Epoch
            )
        );
        // FIJ-006 (b): the active sticky modifiers are held with the button for the whole drag.
        var (deadline, inherits) = step.Deadline(shortcut.Options.MaxHold);
        var item = KeyPlanner.Template(
            step,
            holder,
            HoldOrigin.Toggle,
            origin,
            contact: null,
            deadline,
            inherits
        ) with
        {
            Keys = new ValueList<InjectedKey>(StickyKeys(step, origin)),
            Buttons = MouseButtons.Left,
            Label = step.NameOf(shortcut),
        };
        step.Press(step.State.Keys.Acquire(item), holder, origin);
        StickyPlanner.Consume(step);
        // EJE-007: «{name} · activado · toca otra vez para soltar».
        step.Notice(L.LatchedName(name: step.NameOf(shortcut)));
        step.CountUsage(origin, repeatable: false);
    }
}
