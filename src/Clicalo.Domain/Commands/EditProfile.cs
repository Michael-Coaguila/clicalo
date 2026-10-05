using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Changes a profile's name, icon, binding or compatible mode, keeping its id and shortcuts (ATJ-004, PER-008). An
/// empty name is refused (ATJ-004); consecutive edits of the same profile form one undo step.
/// </summary>
/// <param name="Profile">The new version, with the id of the edited profile; its shortcuts are ignored.</param>
public sealed record EditProfile(Profile Profile) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document
            .Library.ReplaceProfile(Profile)
            .Bind(library =>
                Changes.Recorded(document with { Library = library }, L.Saved, Profile.Id.Value)
            );
    }
}
