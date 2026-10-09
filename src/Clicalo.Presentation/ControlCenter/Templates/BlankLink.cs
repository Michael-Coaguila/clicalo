namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>An option of «Se activa con» of «Perfil vacío» (PLA-010).</summary>
/// <param name="Id">An open app's process, or detect, or none.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its text.</param>
/// <param name="Selected">Whether it is chosen.</param>
public sealed record BlankLink(
    string Id,
    string Icon,
    string Label,
    bool Selected
);
