using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Icons;

/// <summary>
/// One icon of the icon library (<c>data/catalogs/icons.json</c>, the prototype's <c>ICONLIB</c>): the Material Symbols
/// name, its search words and its category, in the order of the file (EDI-004, EDI-005, D14).
/// </summary>
/// <param name="Icon">The icon.</param>
/// <param name="Tags">
/// Its search words as written: the hand-written ones of every language first, then the English keywords of the
/// Material Symbols metadata.
/// </param>
/// <param name="Category">The <see cref="IconCategory.Id"/> of its category; empty when it has none.</param>
/// <param name="Label">
/// The first hand-written word of each language («negrita», «bold»): the name of the icon for UI Automation and voice
/// access in the language of the interface (REG-06); null when the icon has no hand-written words.
/// </param>
public sealed record IconEntry(
    IconRef Icon,
    ValueList<string> Tags,
    string Category = "",
    LocalizedText? Label = null
);
