using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Icons;

/// <summary>
/// One icon of the icon library (<c>data/catalogs/icons.json</c>, the prototype's <c>ICONLIB</c>): the Material Symbols
/// name, its words and its category, in the order of the file (EDI-004, EDI-005, D14).
/// </summary>
/// <param name="Icon">The icon.</param>
/// <param name="Tags">
/// Its hand-written words of every language, as written: what the search and the suggestions of a name use (EDI-005).
/// </param>
/// <param name="Category">The <see cref="IconCategory.Id"/> of its category; empty when it has none.</param>
/// <param name="Label">
/// All its hand-written words of each language, in their order («copiar, duplicar», «copy, duplicate»): the name of the
/// icon for UI Automation and voice access in the language of the interface (REG-06); null when the icon has no
/// hand-written words.
/// </param>
/// <param name="Keywords">
/// The English keywords of the Material Symbols metadata: only the search uses them, so «Word» or «Zoom» do not get
/// the icon whose metadata mentions them (EDI-005 suggests from the words of the library, the prototype's).
/// </param>
public sealed record IconEntry(
    IconRef Icon,
    ValueList<string> Tags,
    string Category = "",
    LocalizedText? Label = null,
    ValueList<string> Keywords = default
);
