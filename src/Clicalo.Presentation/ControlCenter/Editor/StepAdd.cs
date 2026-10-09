using Clicalo.Application.UseCases.Editor;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A + button under the steps (EDI-013).</summary>
/// <param name="Kind">The kind of step it adds.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its name.</param>
public sealed record StepAdd(MacroStepKind Kind, string Icon, string Label);
