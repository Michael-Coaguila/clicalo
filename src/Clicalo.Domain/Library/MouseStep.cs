namespace Clicalo.Domain.Library;

/// <summary>A mouse action at the last external pointer position (EJE-009).</summary>
/// <param name="Op">The action.</param>
public sealed record MouseStep(MouseOp Op) : MacroStep;
