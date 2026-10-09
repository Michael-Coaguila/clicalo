namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>An icon of a picker grid; the chosen one is marked with accentWash, an accent border and the selected state.</summary>
/// <param name="Icon">The Material Symbols name.</param>
/// <param name="Selected">Whether it is the current icon.</param>
public sealed record IconOption(string Icon, bool Selected);
