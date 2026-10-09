using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.KeySafety;

/// <summary>An entry of the logical ledger (SEG-001, blueprint §7.4): what a holder keeps pressed, since when and until when.</summary>
/// <param name="Holder">Who holds it.</param>
/// <param name="Origin">Why.</param>
/// <param name="Shortcut">The shortcut that pressed it, if any (the panic strip names it, SEG-002).</param>
/// <param name="ContactId">The contact that owns it; only its end, a deadline, a terminal event or «Release all» removes it (INV-9).</param>
/// <param name="Keys">The physical keys, in press order.</param>
/// <param name="Buttons">The mouse buttons.</param>
/// <param name="SinceTicks">When it was pressed, in <see cref="TimeProvider"/> ticks.</param>
/// <param name="DeadlineTicks">When it is released automatically, or <see langword="null"/> for «Never» (INV-4).</param>
public sealed record PressedItem(
    HolderId Holder,
    HoldOrigin Origin,
    ShortcutId? Shortcut,
    int? ContactId,
    ValueList<InjectedKey> Keys,
    MouseButtons Buttons,
    long SinceTicks,
    long? DeadlineTicks
)
{
    /// <summary>
    /// Whether the deadline follows the global automatic release limit, so changing that limit recomputes it
    /// (SEG-004); <see langword="false"/> for an item with its own limit or with «Never».
    /// </summary>
    public bool InheritsGlobalLimit { get; init; }

    /// <summary>
    /// What the release notice names (EJE-004, EJE-007): the keys of a Hold under a finger, the name of a Toggle;
    /// <see langword="null"/> for anything else.
    /// </summary>
    public string? Label { get; init; }
}
