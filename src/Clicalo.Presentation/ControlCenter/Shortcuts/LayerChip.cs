namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>A chip of «[panelShows]» (ATJ-001).</summary>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its name.</param>
/// <param name="Meta">What follows it in muted text.</param>
public sealed record LayerChip(string Icon, string Label, string Meta);
