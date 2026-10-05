namespace Clicalo.Domain.Commands;

/// <summary>
/// A command that removes a user entity or a set of data (REG-04): it can only be dispatched with a
/// <c>ConfirmationToken</c> issued by <c>TwoStepConfirm</c> (CLC0010), and the implementations are exactly the closed
/// list of <c>architecture/destructive-operations.json</c> (rule R4). The analyzer binds to it by metadata name
/// (<c>docs/guides/analyzers.md</c>): do not rename or move it.
/// </summary>
public interface IDestructiveCommand : IDocumentCommand;
