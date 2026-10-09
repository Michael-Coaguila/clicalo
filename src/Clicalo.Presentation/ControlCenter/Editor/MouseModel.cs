using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The mouse action of a Mouse shortcut (EDI-012).</summary>
/// <param name="Label">[mouseLabel].</param>
/// <param name="Options">The eight actions in two columns.</param>
/// <param name="SpeedLabel">[speed].</param>
/// <param name="Speeds">The speeds; empty unless the action scrolls.</param>
public sealed record MouseModel(
    string Label,
    ValueList<MouseOption> Options,
    string SpeedLabel,
    ValueList<SpeedOption> Speeds
);
