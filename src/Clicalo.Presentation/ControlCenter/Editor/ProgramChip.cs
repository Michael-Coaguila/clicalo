namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>An installed program of «Elegir programa» (EDI-014).</summary>
/// <param name="Target">What the App field gets when it is chosen.</param>
/// <param name="Name">Its name.</param>
public sealed record ProgramChip(string Target, string Name);
