using System.Collections.Frozen;
using System.Globalization;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Keys;

/// <summary>
/// Writes a combination as the key line of a tile (CUA-007, CUA-008): the label of each key of
/// <c>data/catalogs/keys.json</c> in the interface language, in press order. A sided modifier uses the label of its
/// sided key (Ctrl + right side → «Ctrl der.»). A key without labels is written by its character (<c>char:ñ</c> → «Ñ»)
/// or by its id. Pure.
/// </summary>
public static class KeyChordFormatter
{
    private const string FullSeparator = " + ";
    private const string ShortSeparator = "+";

    private static readonly FrozenDictionary<(KeyId Base, KeySide Side), KeyId> SidedKeys =
        KeyDefinitions
            .All.Where(static d => d.BaseKey is not null)
            .GroupBy(static d => (d.BaseKey!.Value, d.Side))
            .ToFrozenDictionary(static g => g.Key, static g => g.First().Id);

    /// <summary>
    /// <paramref name="chord"/> written in <paramref name="style"/>; the empty chord is the empty string.
    /// </summary>
    /// <param name="chord">The combination.</param>
    /// <param name="labels">The key labels.</param>
    /// <param name="style">Full, short (size S) or spoken (accessible name).</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a label lacks <paramref name="language"/>.</param>
    public static string Format(
        KeyChord chord,
        KeyLabelCatalog labels,
        KeyLabelStyle style,
        LangCode language,
        LangCode fallback
    )
    {
        ArgumentNullException.ThrowIfNull(chord);
        ArgumentNullException.ThrowIfNull(labels);
        if (chord.IsEmpty)
        {
            return string.Empty;
        }

        var parts = new string[chord.Strokes.Count];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = KeyText(chord.Strokes[i], labels, style, language, fallback);
        }

        return string.Join(
            style == KeyLabelStyle.Abbreviated ? ShortSeparator : FullSeparator,
            parts
        );
    }

    /// <summary>The text of one stroke in <paramref name="style"/>.</summary>
    /// <param name="stroke">The stroke.</param>
    /// <param name="labels">The key labels.</param>
    /// <param name="style">Full, short or spoken.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a label lacks <paramref name="language"/>.</param>
    public static string KeyText(
        KeyStroke stroke,
        KeyLabelCatalog labels,
        KeyLabelStyle style,
        LangCode language,
        LangCode fallback
    )
    {
        ArgumentNullException.ThrowIfNull(labels);
        var key =
            stroke.Side != KeySide.Any
            && SidedKeys.TryGetValue((stroke.Key, stroke.Side), out var sided)
                ? sided
                : stroke.Key;
        if (!labels.TryGet(key, out var label))
        {
            return Unlabeled(key);
        }

        var text = style switch
        {
            KeyLabelStyle.Abbreviated => label.Abbreviated ?? label.Label,
            KeyLabelStyle.Spoken => label.Spoken ?? label.Label,
            _ => label.Label,
        };
        return text.Get(language, fallback);
    }

    private static string Unlabeled(KeyId key)
    {
        var value = key.Value ?? string.Empty;
        return key.IsCharacter
            ? value[KeyId.CharacterPrefix.Length..].ToUpper(CultureInfo.InvariantCulture)
            : value;
    }
}
