using Clicalo.Domain.Catalog;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// Projects an icon picker (EDI-004, D14), the same for a shortcut, a profile and Â«Perfil vacÃ­oÂ»: the categories of the
/// collection with Â«TodosÂ» first, and the icons of the chosen one that match the search, all of them.
/// </summary>
internal static class IconPickerProjection
{
    /// <summary>The picker for <paramref name="current"/>.</summary>
    /// <param name="catalog">The icon library.</param>
    /// <param name="current">The icon in use.</param>
    /// <param name="suggested">The suggestions for the name; none for a profile without a name.</param>
    /// <param name="query">The search as typed.</param>
    /// <param name="category">The chosen category, or null for Â«TodosÂ».</param>
    /// <param name="language">The language of the interface, for the names of the categories.</param>
    /// <param name="text">Formats a message in the language of the interface.</param>
    /// <param name="first">
    /// The icons that go first without a search, instead of the featured icons of a shortcut (the 28 of a profile,
    /// ATJ-004); null for the order of the library.
    /// </param>
    public static IconPickerModel Build(
        IconCatalog catalog,
        string current,
        IEnumerable<IconRef> suggested,
        string query,
        string? category,
        LangCode language,
        Func<Message, string> text,
        IEnumerable<IconRef>? first = null
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(suggested);
        ArgumentNullException.ThrowIfNull(text);
        var known =
            category is not null
            && catalog.Categories.Items.Any(c =>
                string.Equals(c.Id, category, StringComparison.Ordinal)
            );
        var chosen = known ? category : null;
        var found = catalog.Search(query);
        if (first is not null && query.Trim().Length == 0)
        {
            var lead = first
                .Where(static icon => !string.IsNullOrEmpty(icon.Name))
                .Distinct()
                .ToList();
            var seen = lead.ToHashSet();
            found = new ValueList<IconRef>([
                .. lead,
                .. found.Items.Where(icon => !seen.Contains(icon)),
            ]);
        }

        ValueList<string> icons;
        if (chosen is null)
        {
            icons = new([.. found.Items.Select(static icon => icon.Name)]);
        }
        else
        {
            var inCategory = catalog
                .InCategory(chosen)
                .Items.Select(static icon => icon.Name)
                .ToHashSet(StringComparer.Ordinal);
            icons = new([
                .. found.Items.Select(static icon => icon.Name).Where(inCategory.Contains),
            ]);
        }

        return new IconPickerModel(
            text(L.ChangeIcon),
            text(L.Close),
            current,
            text(L.IconSug),
            [
                .. suggested.Select(icon => new IconOption(
                    icon.Name,
                    string.Equals(icon.Name, current, StringComparison.Ordinal)
                )),
            ],
            query,
            text(L.IconSearch),
            text(L.SearchDictate),
            [
                new IconCategoryOption(null, text(L.IconAll), chosen is null),
                .. catalog.Categories.Items.Select(c => new IconCategoryOption(
                    c.Id,
                    c.Label.Get(language, LangCode.Es),
                    string.Equals(c.Id, chosen, StringComparison.Ordinal)
                )),
            ],
            icons,
            icons.IsEmpty ? text(L.NoResults) : null
        );
    }
}
