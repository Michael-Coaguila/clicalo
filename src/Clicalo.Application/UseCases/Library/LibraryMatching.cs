using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Library;

/// <summary>
/// Whether a ready action is already in the list (ATJ-010: ✓ instead of ⊕): the same catalog origin, the same
/// combination (REP-001), the same mouse action or the same text. «Same origin» is a catalog reference, never an id
/// prefix (decision of ATJ-010). An empty text matches nothing: «Mi correo» starts empty. Pure.
/// </summary>
public static class LibraryMatching
{
    /// <summary>Whether <paramref name="item"/> of <paramref name="source"/> is already in <paramref name="list"/>.</summary>
    /// <param name="list">The shortcuts of the list in view.</param>
    /// <param name="item">The ready action.</param>
    /// <param name="source">The source of the item's catalog reference.</param>
    public static bool IsAdded(ValueList<Shortcut> list, TemplateShortcut item, string source)
    {
        ArgumentNullException.ThrowIfNull(item);
        var keys = KeysOf(item.Action);
        foreach (var shortcut in list)
        {
            if (
                shortcut.Origin is { } origin
                && string.Equals(origin.Source, source, StringComparison.Ordinal)
                && string.Equals(origin.ItemId, item.ItemId, StringComparison.Ordinal)
            )
            {
                return true;
            }

            if (DuplicateIndex.TryGetKey(shortcut, out var key) && keys.Contains(key))
            {
                return true;
            }

            if (
                shortcut.Action is MouseAction mouse
                && item.Action is MouseAction other
                && mouse.Op == other.Op
            )
            {
                return true;
            }

            if (
                shortcut.Action is TextAction text
                && item.Action is TextAction itemText
                && itemText.Text.Length > 0
                && text.Text.Equals(itemText.Text)
            )
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<CanonicalChord> KeysOf(ShortcutAction action)
    {
        var chords = action switch
        {
            TapAction tap => [tap.Chord, .. tap.Variants.Items.Select(static v => v.Chord)],
            HoldAction hold => [hold.Chord],
            ToggleAction toggle => [toggle.Chord],
            _ => Array.Empty<KeyChord>(),
        };
        var keys = new HashSet<CanonicalChord>();
        foreach (var chord in chords)
        {
            if (CanonicalChord.TryFrom(chord, out var key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }
}
