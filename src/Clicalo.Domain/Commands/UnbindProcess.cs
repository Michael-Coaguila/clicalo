using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Unbinds a process from a profile (ATJ-006); a profile without processes is manual («[linkRemoved]»).
/// </summary>
/// <param name="Profile">The profile.</param>
/// <param name="Process">The process.</param>
public sealed record UnbindProcess(ProfileId Profile, ProcessName Process) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document
            .Library.Unbind(Profile, Process)
            .Bind(library =>
                Changes.Recorded(document with { Library = library }, L.LinkRemoved, null)
            );
    }
}
