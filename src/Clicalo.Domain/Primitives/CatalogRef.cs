namespace Clicalo.Domain.Primitives;

/// <summary>Where a catalog element came from: template or library, its version and the element (DAT-004).</summary>
/// <param name="Source">Template or library identifier (<c>word</c>, <c>library</c>…).</param>
/// <param name="Version">Version of the source when the element was installed.</param>
/// <param name="ItemId">Element identifier inside the source.</param>
public readonly record struct CatalogRef(string Source, string Version, string ItemId);
