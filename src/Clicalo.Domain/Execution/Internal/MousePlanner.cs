using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
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
    public static bool IsScroll(MouseOp op) =>
        op is MouseOp.ScrollUp or MouseOp.ScrollDown or MouseOp.ScrollLeft or MouseOp.ScrollRight;

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
        MouseAction mouse,
        int contactId
    )
    {
        if (!step.CanPress(origin))
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
        var (deadline, _) = step.GlobalDeadline();
        step.State = step.State with
        {
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
        step.CountUsage(origin);
    }

    /// <summary>The scroll timer fired: one more step, or the end of the repeat.</summary>
    public static void ScrollTimer(EngineStep step)
    {
        if (step.State.Scroll is not { } scroll || scroll.NextTicks > step.Now)
        {
            return;
        }

        if (
            (scroll.DeadlineTicks is { } deadline && deadline <= step.Now)
            || !step.CanPress(scroll.Origin)
        )
        {
            step.State = step.State with { Scroll = null };
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
        };
        step.Press(step.State.Keys.Acquire(item), holder, origin);
        StickyPlanner.Consume(step);
        step.Notice(EngineNotices.Latched);
        step.CountUsage(origin);
    }
}
