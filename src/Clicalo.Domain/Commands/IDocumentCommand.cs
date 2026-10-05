using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Domain.Commands;

/// <summary>
/// A pure change of the user document (blueprint §6.3): a small class that uses no ports. What needs I/O is a use
/// case of Application that prepares a pure plan and dispatches a command. Every command returns
/// <see cref="UndoIntent.Record"/> except the exemptions of <c>architecture/undo-exemptions.json</c> (rule R7, REG-07).
/// </summary>
public interface IDocumentCommand
{
    /// <summary>Applies the command; a failure changes nothing and has no effects.</summary>
    /// <param name="document">The current document.</param>
    /// <param name="context">Clock, id source and languages.</param>
    Result<DocumentChange> Apply(UserDocument document, DomainContext context);
}
