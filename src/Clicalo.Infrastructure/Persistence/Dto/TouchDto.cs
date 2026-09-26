namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The touch filter (TAC-001); durations in milliseconds.</summary>
internal sealed record TouchDto
{
    public string? Preset { get; init; }

    public double? DebounceMs { get; init; }

    public int? HitSlopPx { get; init; }

    public int? CancelMovePx { get; init; }

    public double? MinContactMs { get; init; }
}
