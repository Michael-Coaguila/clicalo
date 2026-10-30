using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// A Web, App or Macro shortcut of a backup being imported or restored that the document does not have yet (LOG-008):
/// it is shown with what it opens or runs and is only installed when the person ticks it.
/// </summary>
/// <param name="Id">The shortcut.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Name">Its name, in the interface language.</param>
/// <param name="Detail">What it does: the address, the app or how many steps the macro has.</param>
/// <param name="Checked">Whether the person confirmed it; unticked by default.</param>
public sealed record ReviewRowModel(
    ShortcutId Id,
    string Icon,
    string Name,
    string Detail,
    bool Checked
);
