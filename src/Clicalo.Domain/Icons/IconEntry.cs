using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Icons;

/// <summary>
/// One icon of the icon library (<c>data/catalogs/icons.json</c>, the prototype's <c>ICONLIB</c>): the Material Symbols
/// name and its search words in every language, in the order of the file (EDI-004, EDI-005).
/// </summary>
/// <param name="Icon">The icon.</param>
/// <param name="Tags">Its search words, Spanish and English together, as written.</param>
public sealed record IconEntry(IconRef Icon, ValueList<string> Tags);
