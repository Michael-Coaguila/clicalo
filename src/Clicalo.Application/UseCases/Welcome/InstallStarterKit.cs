using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// Installs what the welcome step «¿Qué apps usas más?» marks (BIE-006, BIE-009) in a single undoable step, only
/// adding (<see cref="WelcomeKit.Install"/>), with the programs language of the settings.
/// </summary>
/// <param name="Content">The kit, the seed and the templates.</param>
/// <param name="Selection">The marked options.</param>
public sealed record InstallStarterKit(StarterContent Content, StarterSelection Selection)
    : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return WelcomeKit
            .Install(
                document.Library,
                Content,
                Selection,
                document.Settings.Keyboard.AppsLanguage,
                context.Ids
            )
            .Map(library => new DocumentChange(
                document with
                {
                    Library = library,
                },
                [],
                new UndoIntent.Record(L.Welcome.Key, null),
                new BackupRequirement.None()
            ));
    }
}
