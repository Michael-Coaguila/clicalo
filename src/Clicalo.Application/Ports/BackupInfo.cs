using Clicalo.Domain.Document;

namespace Clicalo.Application.Ports;

/// <summary>A backup as listed: its counts are those of the backup itself (COP-004).</summary>
/// <param name="Id">The backup.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="CreatedAt">When it was taken.</param>
/// <param name="Profiles">Profiles in it.</param>
/// <param name="Shortcuts">Shortcuts in it.</param>
public sealed record BackupInfo(
    BackupId Id,
    BackupKind Kind,
    DateTimeOffset CreatedAt,
    int Profiles,
    int Shortcuts
);
