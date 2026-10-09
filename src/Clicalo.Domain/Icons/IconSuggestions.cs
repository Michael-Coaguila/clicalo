using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Icons.Internal;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Icons;

/// <summary>
/// The algorithm <c>suggestIcons(nombre, teclas)</c> of EDI-005, the only one: the editor puts its first suggestion on a
/// shortcut whose icon follows the name (EDI-003) and shows all of them above the icon grid (EDI-004); the profile
/// editor and «Perfil vacío» use it without keys. Pure.
/// </summary>
public static class IconSuggestions
{
    /// <summary>
    /// Up to <c>Timings.Icons.IconSuggestionsMax</c> icons for a name and a combination:
    /// <list type="number">
    /// <item>the name in lower case and without diacritics;</item>
    /// <item>its alphanumeric words of at least <c>Timings.Icons.IconWordMinLength</c> letters;</item>
    /// <item>the icon of the combination in the programs language first, if any;</item>
    /// <item>then, in library order and without repeating, each icon with a word x of its tags that starts with a
    /// word w of the name, or such that w starts with x and x has at least
    /// <c>Timings.Icons.IconReverseMatchMinLength</c> letters.</item>
    /// </list>
    /// </summary>
    /// <param name="name">The name as typed; null or empty gives only the combination icon.</param>
    /// <param name="chord">The combination; null for none (a profile, a macro).</param>
    /// <param name="appsLanguage">The language of the programs (keyboard settings).</param>
    /// <param name="catalog">The icon library.</param>
    /// <param name="combos">The icons of combinations.</param>
    public static ValueList<IconRef> Suggest(
        string? name,
        KeyChord? chord,
        LangCode appsLanguage,
        IconCatalog catalog,
        ComboIconTable combos
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(combos);
        var max = Timings.Icons.IconSuggestionsMax;
        var result = ImmutableArray.CreateBuilder<IconRef>();
        if (combos.TryGet(chord, appsLanguage, out var comboIcon))
        {
            result.Add(comboIcon);
        }

        var words = IconText
            .Words(IconText.Fold(name))
            .Where(static w => w.Length >= Timings.Icons.IconWordMinLength)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (words.Length > 0)
        {
            foreach (var entry in catalog.Entries)
            {
                if (result.Count >= max)
                {
                    break;
                }

                if (!result.Contains(entry.Icon) && Matches(entry, words))
                {
                    result.Add(entry.Icon);
                }
            }
        }

        return new ValueList<IconRef>(
            result.Count > max ? result.ToImmutable()[..max] : result.ToImmutable()
        );
    }

    private static bool Matches(IconEntry entry, string[] words)
    {
        foreach (var tag in entry.Tags)
        {
            foreach (var x in IconText.Words(IconText.Fold(tag)))
            {
                foreach (var w in words)
                {
                    if (
                        x.StartsWith(w, StringComparison.Ordinal)
                        || (
                            w.StartsWith(x, StringComparison.Ordinal)
                            && x.Length >= Timings.Icons.IconReverseMatchMinLength
                        )
                    )
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
