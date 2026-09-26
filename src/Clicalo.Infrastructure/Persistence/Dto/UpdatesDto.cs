namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>Updates (ACT-*).</summary>
internal sealed record UpdatesDto
{
    public bool? Auto { get; init; }

    public bool? AskBefore { get; init; }

    public bool? BackupBefore { get; init; }

    public string? Channel { get; init; }
}
