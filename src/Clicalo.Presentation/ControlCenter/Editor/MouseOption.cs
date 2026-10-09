using Clicalo.Domain.Library;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>One of the eight mouse actions (EDI-012).</summary>
/// <param name="Op">The action.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its name.</param>
/// <param name="AccessibleName">Its name for screen readers.</param>
/// <param name="Selected">Whether it is the chosen one.</param>
public sealed record MouseOption(
    MouseOp Op,
    string Icon,
    string Label,
    string AccessibleName,
    bool Selected
);
