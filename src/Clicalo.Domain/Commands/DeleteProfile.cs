using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Deletes a profile and its shortcuts (REG-04, ATJ-004); General cannot be deleted (I3). A last profile that pointed
/// to it becomes General (PER-008); <see cref="ProfileRemoved"/> lets the panel and the editor leave it.
/// </summary>
/// <param name="Id">The profile.</param>
public sealed record DeleteProfile(ProfileId Id) : IDestructiveCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document
            .Library.RemoveProfile(Id)
            .Bind(library =>
            {
                var settings =
                    document.Settings.LastProfile == Id
                        ? document.Settings with
                        {
                            LastProfile = ProfileId.General,
                        }
                        : document.Settings;
                return Changes.Recorded(
                    document with
                    {
                        Library = library,
                        Settings = settings,
                    },
                    L.ProfDeleted,
                    null,
                    new ProfileRemoved(Id)
                );
            });
    }
}
