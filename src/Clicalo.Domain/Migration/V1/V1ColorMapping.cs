using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.Migration.V1;

/// <summary>The category a v1 colour becomes (catalog §7.4, PQ-08).</summary>
/// <param name="Category">The colour category.</param>
/// <param name="Origin">Whether it was missing, from the palette, custom or invalid.</param>
/// <param name="Rgb">The colour as <c>#RRGGBB</c> in upper case, or <see langword="null"/> when invalid.</param>
internal sealed record V1ColorMapping(CategoryId Category, V1ColorOrigin Origin, string? Rgb)
{
    /// <summary>Whether the report has to mention it: custom and invalid colours (PQ-08, EC-MIG-05).</summary>
    public bool IsReported => Origin is V1ColorOrigin.Custom or V1ColorOrigin.Invalid;
}
