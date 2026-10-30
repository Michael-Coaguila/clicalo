using Clicalo.Application.Ports;
using Clicalo.Domain.Document;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// A backup of the history read to be restored whose Web, App or Macro shortcuts still have to be confirmed one by one
/// (LOG-008), before [Restaurar] applies it.
/// </summary>
/// <param name="Id">The backup.</param>
/// <param name="Document">Its document, valid; nothing in it has run (LOG-006).</param>
public sealed record RestoreReview(BackupId Id, UserDocument Document);
