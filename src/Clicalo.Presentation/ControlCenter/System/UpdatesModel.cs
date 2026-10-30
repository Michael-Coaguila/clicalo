using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>The tab «Actualizaciones» (ACT-001 to ACT-005).</summary>
/// <param name="Card">The state card.</param>
/// <param name="Switches">«Actualizar automáticamente», «Avisar antes de instalar», «Copia antes de actualizar».</param>
/// <param name="Channel">[channel].</param>
/// <param name="ChannelDescription">[channelD].</param>
/// <param name="Channels">Estable and Beta.</param>
/// <param name="WhatsNew">[whatsNew].</param>
/// <param name="Notes">The news per version, newest first.</param>
/// <param name="Rollback">The row to go back; null when hidden.</param>
public sealed record UpdatesModel(
    UpdateCardModel Card,
    ValueList<SwitchModel> Switches,
    string Channel,
    string ChannelDescription,
    ValueList<ChannelOption> Channels,
    string WhatsNew,
    ValueList<NotesModel> Notes,
    RollbackModel? Rollback
);
