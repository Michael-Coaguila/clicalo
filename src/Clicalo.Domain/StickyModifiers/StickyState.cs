using Clicalo.Domain.Keys;

namespace Clicalo.Domain.StickyModifiers;

/// <summary>The levels of the four sticky modifiers of the panel row (FIJ-005): Ctrl, Alt, Shift and Win.</summary>
/// <param name="Ctrl">Ctrl.</param>
/// <param name="Alt">Alt.</param>
/// <param name="Shift">Shift.</param>
/// <param name="Win">Win.</param>
public sealed record StickyState(
    StickyLevel Ctrl,
    StickyLevel Alt,
    StickyLevel Shift,
    StickyLevel Win
)
{
    /// <summary>Every modifier released.</summary>
    public static StickyState Empty { get; } =
        new(StickyLevel.Off, StickyLevel.Off, StickyLevel.Off, StickyLevel.Off);

    /// <summary>The modifiers in their canonical send order (FIJ-006, EC-EJE-06): Ctrl, Alt, Shift, Win.</summary>
    public static IReadOnlyList<ModifierKind> Order { get; } =
    [ModifierKind.Ctrl, ModifierKind.Alt, ModifierKind.Shift, ModifierKind.Win];

    /// <summary>Whether every modifier is released.</summary>
    public bool IsEmpty => this == Empty;

    /// <summary>The active modifiers (once or locked), in send order.</summary>
    public IEnumerable<ModifierKind> Active => Order.Where(m => LevelOf(m) != StickyLevel.Off);

    /// <summary>The level of <paramref name="modifier"/>.</summary>
    /// <param name="modifier">The modifier.</param>
    public StickyLevel LevelOf(ModifierKind modifier) =>
        modifier switch
        {
            ModifierKind.Ctrl => Ctrl,
            ModifierKind.Alt => Alt,
            ModifierKind.Shift => Shift,
            _ => Win,
        };

    /// <summary>The state with <paramref name="modifier"/> at <paramref name="level"/>.</summary>
    /// <param name="modifier">The modifier.</param>
    /// <param name="level">Its new level.</param>
    public StickyState With(ModifierKind modifier, StickyLevel level) =>
        modifier switch
        {
            ModifierKind.Ctrl => this with { Ctrl = level },
            ModifierKind.Alt => this with { Alt = level },
            ModifierKind.Shift => this with { Shift = level },
            _ => this with { Win = level },
        };

    /// <summary>A tap on <paramref name="modifier"/>: 0 → 1 → 2 → 0 (FIJ-005).</summary>
    /// <param name="modifier">The modifier.</param>
    public StickyState Advance(ModifierKind modifier) =>
        With(
            modifier,
            LevelOf(modifier) switch
            {
                StickyLevel.Off => StickyLevel.Once,
                StickyLevel.Once => StickyLevel.Locked,
                _ => StickyLevel.Off,
            }
        );

    /// <summary>After a key or mouse action: the modifiers at «once» are released, the locked ones stay (FIJ-006).</summary>
    public StickyState AfterUse() => new(Used(Ctrl), Used(Alt), Used(Shift), Used(Win));

    private static StickyLevel Used(StickyLevel level) =>
        level == StickyLevel.Once ? StickyLevel.Off : level;
}
