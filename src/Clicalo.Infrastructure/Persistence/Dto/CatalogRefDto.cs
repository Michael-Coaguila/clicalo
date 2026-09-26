namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>Where a catalog item came from (DAT-004).</summary>
internal sealed record CatalogRefDto
{
    public string? Source { get; init; }

    public string? Version { get; init; }

    public string? Item { get; init; }
}
