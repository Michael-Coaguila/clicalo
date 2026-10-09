using System.Collections.Frozen;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Icons;

/// <summary>
/// The icon a combination suggests in each language of the programs (<c>data/catalogs/combo-icons.&lt;lang&gt;.json</c>,
/// the prototype's <c>KEYICON</c>; EDI-005 step 3): Ctrl+G saves in Spanish programs, Ctrl+S in English ones.
/// Combinations compare as canonical keys without the side of the modifiers, so «Ctrl izq. + C» also finds the icon of
/// Ctrl+C.
/// </summary>
public sealed class ComboIconTable
{
    private readonly FrozenDictionary<(LangCode Language, string Key), IconRef> _icons;

    /// <summary>Creates the table.</summary>
    /// <param name="entries">The icon of each combination in each programs language; the first one of a pair wins.</param>
    public ComboIconTable(IEnumerable<(LangCode Language, KeyChord Chord, IconRef Icon)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var icons = new Dictionary<(LangCode, string), IconRef>();
        foreach (var (language, chord, icon) in entries)
        {
            if (chord is not null && KeyOf(chord) is { } key && !string.IsNullOrEmpty(icon.Name))
            {
                icons.TryAdd((language, key), icon);
            }
        }

        _icons = icons.ToFrozenDictionary();
    }

    /// <summary>A table without combinations.</summary>
    public static ComboIconTable Empty { get; } = new([]);

    /// <summary>The icon of <paramref name="chord"/> for programs in <paramref name="appsLanguage"/>.</summary>
    /// <param name="chord">The combination.</param>
    /// <param name="appsLanguage">The language of the programs (keyboard settings).</param>
    /// <param name="icon">Its icon.</param>
    public bool TryGet(KeyChord? chord, LangCode appsLanguage, out IconRef icon)
    {
        icon = default;
        return chord is not null
            && KeyOf(chord) is { } key
            && _icons.TryGetValue((appsLanguage, key), out icon);
    }

    private static string? KeyOf(KeyChord chord) =>
        CanonicalChord.TryFrom(chord, out var canonical)
            ? canonical.ForBlockedComparison().ToStableString()
            : null;
}
