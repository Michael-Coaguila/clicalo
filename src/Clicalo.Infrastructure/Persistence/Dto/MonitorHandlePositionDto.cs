namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The Tab handle position on one edge of one monitor (PES-016, schema 1.1, ADR-0028).</summary>
internal sealed record MonitorHandlePositionDto
{
    public string? Monitor { get; init; }

    public string? Side { get; init; }

    public int? Pos { get; init; }
}
