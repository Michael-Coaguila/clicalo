using Clicalo.Application.UseCases.Editor;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A position button of «Más opciones» (EDI-016).</summary>
/// <param name="Move">The button.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its name.</param>
/// <param name="Enabled">Whether it would move the shortcut.</param>
public sealed record PositionOption(PositionMove Move, string Icon, string Label, bool Enabled);
