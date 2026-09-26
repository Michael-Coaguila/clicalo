using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>Moves a profile to another position among the profiles (docs/02 <c>order</c>).</summary>
/// <param name="Id">The profile.</param>
/// <param name="At">Position once the profile has left its place.</param>
public sealed record MoveProfile(ProfileId Id, ListPosition At) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document
            .Library.MoveProfile(Id, At)
            .Bind(library => Changes.Recorded(document with { Library = library }, L.Saved, null));
    }
}
