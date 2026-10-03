using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The content of the «Basics» option (<c>data/content/seed.json</c>, CAT-003): the universal shortcuts of the Always
/// visible row and of General. General itself, with this name and icon, exists in every document, even an empty one.
/// </summary>
/// <param name="CatalogVersion">The version of the seed; stored in the catalog reference (DAT-004).</param>
/// <param name="AlwaysVisible">The shortcuts of the Always visible row.</param>
/// <param name="GeneralName">The name of General.</param>
/// <param name="GeneralIcon">The icon of General.</param>
/// <param name="General">The shortcuts of General.</param>
public sealed record SeedContent(
    int CatalogVersion,
    ValueList<TemplateShortcut> AlwaysVisible,
    LocalizedText GeneralName,
    IconRef GeneralIcon,
    ValueList<TemplateShortcut> General
)
{
    /// <summary>The source of the catalog reference of every seed shortcut (DAT-004).</summary>
    public const string Source = "seed";
}
