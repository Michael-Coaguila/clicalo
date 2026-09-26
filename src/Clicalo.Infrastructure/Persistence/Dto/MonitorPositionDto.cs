namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The panel position on one monitor.</summary>
internal sealed record MonitorPositionDto
{
    public string? Monitor { get; init; }

    public int? X { get; init; }

    public int? Y { get; init; }
}
