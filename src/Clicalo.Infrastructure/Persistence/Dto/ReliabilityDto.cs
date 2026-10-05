namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>Start-up and stability (SIS-*).</summary>
internal sealed record ReliabilityDto
{
    public bool? StartWithWindows { get; init; }

    public bool? AutoBackup { get; init; }

    public bool? CrashRecovery { get; init; }

    public bool? SingleInstance { get; init; }

    public bool? RunAsAdmin { get; init; }
}
