using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Binds a process to a profile (ATJ-006, ATJ-008). A process another profile has is only taken with
/// <paramref name="TakeOver"/>, after the user confirmed (ATJ-007); without it the command fails with
/// <c>library.process.bound</c> and the UI asks. General never has a process (I4).
/// </summary>
/// <param name="Profile">The profile.</param>
/// <param name="Process">The process.</param>
/// <param name="TakeOver">Whether to take the process from the profile that has it.</param>
public sealed record BindProcess(ProfileId Profile, ProcessName Process, bool TakeOver)
    : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var owner = document.Library.ProfileFor(Process)?.Id;
        return document
            .Library.Bind(Profile, Process, TakeOver)
            .Bind(library =>
                Changes.Recorded(
                    document with
                    {
                        Library = library,
                    },
                    L.LinkedTo,
                    null,
                    new ProcessBound(Profile, Process, owner == Profile ? null : owner)
                )
            );
    }
}
