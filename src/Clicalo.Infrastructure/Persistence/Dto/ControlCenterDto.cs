namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>Size, position and monitor of the Control Center, in DIP (CCM-001, schema 1.1, ADR-0028).</summary>
internal sealed record ControlCenterDto
{
    public string? Monitor { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public double? Width { get; init; }

    public double? Height { get; init; }

    public bool? Maximized { get; init; }
}
