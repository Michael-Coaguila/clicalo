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
public sealed record IconEntry(IconRef Icon, ValueList<string> Tags, string Category = "");
