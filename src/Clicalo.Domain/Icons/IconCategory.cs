using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Icons;

/// <summary>
/// One category of the icon library (<c>data/catalogs/icons.json</c> → <c>categories</c>): the categories of the
/// Material Symbols metadata, in the order of the picker (D14).
/// </summary>
/// <param name="Id">The identifier that <see cref="IconEntry.Category"/> names.</param>
/// <param name="Label">The name shown in each language.</param>
public sealed record IconCategory(string Id, LocalizedText Label);
