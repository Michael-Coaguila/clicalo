using Clicalo.Domain.Library;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A scroll speed (EDI-012).</summary>
/// <param name="Speed">The speed.</param>
/// <param name="Label">Its name.</param>
/// <param name="Selected">Whether it is the chosen one.</param>
public sealed record SpeedOption(ScrollSpeed Speed, string Label, bool Selected);
