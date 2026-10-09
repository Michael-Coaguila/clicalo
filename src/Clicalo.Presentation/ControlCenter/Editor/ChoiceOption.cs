namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>An option of a segmented choice of «Más opciones» (EDI-016).</summary>
/// <param name="Id">The option.</param>
/// <param name="Label">Its name.</param>
/// <param name="Selected">Whether it is chosen.</param>
public sealed record ChoiceOption(int Id, string Label, bool Selected);
