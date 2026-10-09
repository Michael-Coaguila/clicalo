using System.Collections.Immutable;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Search;

/// <summary>
/// The panel search (BUS-004, BUS-005): every shortcut of Always visible and of every profile, in that order, whose name
/// in any language (Spanish and English) or whose shown combination contains the query, without case or accents. The
/// content of a Text shortcut is never searched (privacy, DIS-41): only its name. Pure.
/// </summary>
public static class ShortcutSearch
{
    /// <summary>Whether <paramref name="query"/> searches at all: an empty or blank field shows the normal panel (BUS-005).</summary>
    /// <param name="query">The text of the search field.</param>
    public static bool IsActive(string? query) => !string.IsNullOrWhiteSpace(query);

    /// <summary>The results of <paramref name="query"/> in display order; empty when the query is blank.</summary>
    /// <param name="library">The shortcuts.</param>
    /// <param name="query">The text of the search field; surrounding spaces are ignored.</param>
    /// <param name="combination">
    /// The combination a tile shows for a shortcut («Ctrl + N»), with the key names of the interface; null or empty
    /// when it shows none. The search matches what the user sees, so the labels stay in the catalogs (R-04).
    /// </param>
    public static ImmutableArray<SearchHit> Find(
        ShortcutLibrary library,
        string? query,
        Func<Shortcut, string?> combination
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(combination);
        if (!IsActive(query))
        {
            return [];
        }

        var folded = SearchText.Fold(query!.Trim());
        var hits = ImmutableArray.CreateBuilder<SearchHit>();
        foreach (var located in library.EnumerateShortcuts())
        {
            var shortcut = located.Shortcut;
            if (Matches(shortcut, folded, combination))
            {
                hits.Add(
                    new SearchHit(
                        shortcut,
                        located.Location.List is ListRef.InProfile profile ? profile.Id : null
                    )
                );
            }
        }

        return hits.ToImmutable();
    }

    private static bool Matches(
        Shortcut shortcut,
        string folded,
        Func<Shortcut, string?> combination
    )
    {
        foreach (var name in shortcut.Name.Values.Values)
        {
            if (SearchText.Contains(name, folded))
            {
                return true;
            }
        }

        return SearchText.Contains(combination(shortcut), folded);
    }
}
