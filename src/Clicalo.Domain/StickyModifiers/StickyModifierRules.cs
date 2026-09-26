using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.StickyModifiers;

/// <summary>
/// How the active sticky modifiers join the next action (FIJ-006, blueprint §7.3 «StickyModifiers.Compose»): Ctrl, Alt,
/// Shift and Win first, in that order, then the keys of the button without repeating a modifier it already has
/// (EC-EJE-06). The same rule applies to the panel's mouse clicks (Ctrl+click). Texts, web, apps and macros do not
/// take them and they stay pending.
/// </summary>
public static class StickyModifierRules
{
    /// <summary>The combination to send: the active modifiers first, then <paramref name="strokes"/> without repeats.</summary>
    /// <param name="strokes">The button's combination, in press order.</param>
    /// <param name="sticky">The sticky modifiers.</param>
    public static ValueList<KeyStroke> Compose(ValueList<KeyStroke> strokes, StickyState sticky)
    {
        ArgumentNullException.ThrowIfNull(sticky);
        var active = sticky.Active.ToHashSet();
        if (active.Count == 0)
        {
            return strokes;
        }

        var composed = new List<KeyStroke>(active.Count + strokes.Count);
        composed.AddRange(sticky.Active.Select(Stroke));
        foreach (var stroke in strokes)
        {
            var modifier = ModifierOf(stroke);
            if (stroke.Side == KeySide.Any && modifier is { } kind && active.Contains(kind))
            {
                continue;
            }

            composed.Add(stroke);
        }

        return ValueListBuilder.From(composed);
    }

    /// <summary>The stroke a sticky modifier is sent as: its generic (left) key.</summary>
    /// <param name="modifier">The modifier.</param>
    public static KeyStroke Stroke(ModifierKind modifier) =>
        new(
            modifier switch
            {
                ModifierKind.Ctrl => KeyIds.Ctrl,
                ModifierKind.Alt => KeyIds.Alt,
                ModifierKind.Shift => KeyIds.Shift,
                _ => KeyIds.Win,
            }
        );

    private static ModifierKind? ModifierOf(KeyStroke stroke) =>
        KeyDefinitions.TryGet(stroke.Key, out var definition) ? definition.Modifier : null;
}
