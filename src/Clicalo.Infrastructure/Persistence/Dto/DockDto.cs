namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The tab view (GEN-010, docs/02 <c>dock</c>).</summary>
internal sealed record DockDto
{
    public string? Side { get; init; }

    public HandlePositionsDto? HandlePosBySide { get; init; }

    public bool? HandleLocked { get; init; }

    public bool? PinOpen { get; init; }

    public bool? Gutter { get; init; }

    public int? PerPage { get; init; }

    public bool? CoachDone { get; init; }
}
