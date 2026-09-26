using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.StickyModifiers;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// The sticky modifiers row (FIJ-005, FIJ-006): each tap moves a modifier 0 → 1 → 2 → 0; an active one is in the
/// ledger (SEG-001: the panic strip shows it, deadlines and «Release all» apply) as an item with no physical key, and
/// joins the next Tap, Hold, Toggle or panel mouse click (<see cref="StickyModifierRules.Compose"/>); texts, web, apps
/// and macros leave it pending. After such an action the ones at «once» are released and the locked ones stay.
/// </summary>
internal static class StickyPlanner
{
    public static void Tap(EngineStep step, ModifierKind modifier)
    {
        var next = step.State.Sticky.Advance(modifier);
        var holder = HolderId.ForSticky(modifier);
        var level = next.LevelOf(modifier);
        if (level == StickyLevel.Off)
        {
            step.Release(step.State.Keys.Release(holder), holder);
        }
        else if (!step.State.Keys.Items.ContainsKey(holder))
        {
            var (deadline, inherits) = step.GlobalDeadline();
            var item = new PressedItem(
                holder,
                HoldOrigin.Sticky,
                Shortcut: null,
                ContactId: null,
                [],
                MouseButtons.None,
                step.Now,
                deadline
            )
            {
                InheritsGlobalLimit = inherits,
            };
            step.State = step.State with { Keys = step.State.Keys.Acquire(item).Ledger };
        }

        step.State = step.State with { Sticky = next };
        step.Notice(
            level switch
            {
                StickyLevel.Once => EngineNotices.StickyOnce,
                StickyLevel.Locked => EngineNotices.StickyLocked,
                _ => EngineNotices.StickyOff,
            }
        );
    }

    /// <summary>The row was switched off: every sticky modifier goes (FIJ-005).</summary>
    public static void Clear(EngineStep step)
    {
        foreach (var modifier in step.State.Sticky.Active.ToList())
        {
            var holder = HolderId.ForSticky(modifier);
            step.Release(step.State.Keys.Release(holder), holder);
        }

        step.State = step.State with { Sticky = StickyState.Empty };
    }

    /// <summary>The combination with the active sticky modifiers first (FIJ-006, EC-EJE-06).</summary>
    public static ValueList<KeyStroke> Compose(EngineStep step, ValueList<KeyStroke> strokes) =>
        StickyModifierRules.Compose(strokes, step.State.Sticky);

    /// <summary>The strokes of the active sticky modifiers alone (a panel mouse click, FIJ-006 b).</summary>
    public static ValueList<KeyStroke> Active(EngineStep step) =>
        StickyModifierRules.Compose([], step.State.Sticky);

    /// <summary>After a key or mouse action: the modifiers at «once» are released, the locked ones stay.</summary>
    public static void Consume(EngineStep step)
    {
        var sticky = step.State.Sticky;
        foreach (var modifier in StickyState.Order)
        {
            if (sticky.LevelOf(modifier) == StickyLevel.Once)
            {
                var holder = HolderId.ForSticky(modifier);
                step.Release(step.State.Keys.Release(holder), holder);
            }
        }

        step.State = step.State with { Sticky = sticky.AfterUse() };
    }

    /// <summary>A sticky modifier's holder reached its deadline (SEG-004): the modifier is released.</summary>
    public static void Expired(EngineStep step, HolderId holder)
    {
        foreach (var modifier in StickyState.Order)
        {
            if (HolderId.ForSticky(modifier) == holder)
            {
                step.State = step.State with
                {
                    Sticky = step.State.Sticky.With(modifier, StickyLevel.Off),
                };
            }
        }
    }
}
