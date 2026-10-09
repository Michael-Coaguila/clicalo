using Clicalo.Domain.Messages;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// A message for the status bar of the Control Center (CCM-003): localized when painted, with its icon, and whether
/// it offers [undo] (when the undo history is not empty).
/// </summary>
/// <param name="Text">The message.</param>
/// <param name="Icon">Its Material Symbols icon.</param>
/// <param name="CanUndo">Whether the change it reports can be undone.</param>
/// <param name="IsWarning">Whether it is a warning (assertive) rather than a notice (polite).</param>
/// <param name="UndoName">What [undo] says it undoes («Deshacer cambios en {nombre}», EDI-021); null for [undo].</param>
public sealed record WorkspaceNotice(
    Message Text,
    string Icon,
    bool CanUndo,
    bool IsWarning,
    Message? UndoName = null
);
