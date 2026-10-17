namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A category of the icon picker (D14).</summary>
/// <param name="Id">The category, or null for «Todos».</param>
/// <param name="Label">Its name in the language of the interface.</param>
/// <param name="Selected">Whether the grid shows it.</param>
public sealed record IconCategoryOption(string? Id, string Label, bool Selected);
