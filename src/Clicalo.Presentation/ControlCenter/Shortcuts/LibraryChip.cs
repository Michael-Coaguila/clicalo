namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>A category chip of «Añadir atajo» (ATJ-010).</summary>
/// <param name="Id">The category.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its name.</param>
/// <param name="Selected">Whether it is shown.</param>
public sealed record LibraryChip(string Id, string Icon, string Label, bool Selected);
