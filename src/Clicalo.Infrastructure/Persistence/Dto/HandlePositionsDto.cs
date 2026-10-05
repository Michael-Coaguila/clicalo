namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The tab handle position per side, in percent.</summary>
internal sealed record HandlePositionsDto
{
    public int? Right { get; init; }

    public int? Left { get; init; }

    public int? Top { get; init; }

    public int? Bottom { get; init; }
}
