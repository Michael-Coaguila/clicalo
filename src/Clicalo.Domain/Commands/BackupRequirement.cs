using Clicalo.Domain.Document;

namespace Clicalo.Domain.Commands;

/// <summary>Whether the store takes a backup of the current document before applying a change (§6.8, DAT-006).</summary>
public abstract record BackupRequirement
{
    private BackupRequirement() { }

    /// <summary>No backup.</summary>
    public sealed record None : BackupRequirement;

    /// <summary>A backup of the document before the change (in memory; the disk write is queued).</summary>
    /// <param name="Kind">The kind of backup.</param>
    public sealed record BeforeApply(BackupKind Kind) : BackupRequirement;
}
