using Clicalo.Domain.Keys;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The edits of the combination box (EDI-007, EDI-008, EDI-009), in press order (EJE-003, EDI-010). Pure.
/// </summary>
/// <remarks>
/// A key is the same key whatever its side: there is a single mechanism for the side, an attribute of the stroke
/// (EDI-009, DIS-55). The keys of the «Izq. / Der.» group are shortcuts of that attribute: tapping «Ctrl izq.» on a
/// combination that has «Ctrl» gives that Ctrl the left side in its place; tapping it again removes it. Tapping a key
/// without a side (a modifier button or any other key) adds it at the end or, when the combination has it, removes it.
/// </remarks>
public static class ChordEdits
{
    /// <summary>A tap on a key of the picker or on a modifier button.</summary>
    /// <param name="chord">The combination.</param>
    /// <param name="key">The catalog key tapped (a sided key such as <c>lctrl</c> carries its side).</param>
    public static KeyChord Tap(KeyChord chord, KeyId key)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var tapped = Normalize(key);
        var strokes = chord.Strokes.Items;
        var index = IndexOf(chord, tapped.Key);
        if (index < 0)
        {
            return KeyChord.Create([.. strokes, tapped]);
        }

        var present = strokes[index];
        if (tapped.Side == KeySide.Any || present.Side == tapped.Side)
        {
            return KeyChord.Create(strokes.RemoveAt(index));
        }

        return KeyChord.Create(strokes.SetItem(index, present with { Side = tapped.Side }));
    }

    /// <summary>Whether the picker cell of <paramref name="key"/> shows as chosen (accentWash and selected state).</summary>
    /// <param name="chord">The combination.</param>
    /// <param name="key">The catalog key of the cell.</param>
    public static bool IsChosen(KeyChord chord, KeyId key)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var cell = Normalize(key);
        var index = IndexOf(chord, cell.Key);
        return index >= 0 && (cell.Side == KeySide.Any || chord.Strokes[index].Side == cell.Side);
    }

    /// <summary>The × of a key chip: removes the key at <paramref name="index"/>.</summary>
    /// <param name="chord">The combination.</param>
    /// <param name="index">Zero-based position in press order.</param>
    public static KeyChord RemoveAt(KeyChord chord, int index)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return index < 0 || index >= chord.Strokes.Count
            ? chord
            : KeyChord.Create(chord.Strokes.Items.RemoveAt(index));
    }

    /// <summary>⌫ [backKey]: removes the last key.</summary>
    /// <param name="chord">The combination.</param>
    public static KeyChord RemoveLast(KeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return RemoveAt(chord, chord.Strokes.Count - 1);
    }

    private static KeyStroke Normalize(KeyId key) =>
        KeyChord.Create([new KeyStroke(key)]) is { IsEmpty: false } single
            ? single.Strokes[0]
            : new KeyStroke(key);

    private static int IndexOf(KeyChord chord, KeyId key)
    {
        for (var i = 0; i < chord.Strokes.Count; i++)
        {
            if (string.Equals(chord.Strokes[i].Key.Value, key.Value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
