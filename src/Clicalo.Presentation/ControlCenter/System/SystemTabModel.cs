namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>A tab of «Sistema» with its state (SIS-001).</summary>
/// <param name="Tab">The tab.</param>
/// <param name="Icon">Its icon (<c>new_releases</c> in warn while there is a new version).</param>
/// <param name="Label">Its name.</param>
/// <param name="Status">Its state: «v2.0.0 · al día», «Última: Hoy, 09:12», «Inicia con Windows»…</param>
/// <param name="Warn">Whether the state is a warning (a new version).</param>
/// <param name="Selected">Whether it is the tab in view.</param>
public sealed record SystemTabModel(
    SystemTab Tab,
    string Icon,
    string Label,
    string Status,
    bool Warn,
    bool Selected
);
