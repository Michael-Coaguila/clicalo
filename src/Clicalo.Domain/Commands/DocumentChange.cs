using System.Collections.Immutable;
using Clicalo.Domain.Document;

namespace Clicalo.Domain.Commands;

/// <summary>The result of a command (blueprint §6.3).</summary>
/// <param name="Next">The new document; the store raises its revision.</param>
/// <param name="Events">What happened, for coordinators.</param>
/// <param name="Undo">How the change enters the undo history.</param>
/// <param name="Backup">Whether a backup is taken before applying it.</param>
public sealed record DocumentChange(
    UserDocument Next,
    ImmutableArray<DomainEvent> Events,
    UndoIntent Undo,
    BackupRequirement Backup
);
