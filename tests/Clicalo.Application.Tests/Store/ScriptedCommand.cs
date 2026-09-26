using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Application.Tests.Store;

/// <summary>A test command that returns whatever change its function builds, or throws.</summary>
/// <param name="Build">Builds the change from the current document.</param>
internal sealed record ScriptedCommand(Func<UserDocument, DocumentChange> Build) : IDocumentCommand
{
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context) =>
        Results.Ok(Build(document));
}
