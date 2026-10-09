namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>A way out of an AI error card (PLA-006).</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Label">Its text.</param>
public sealed record ErrorAction(
    ErrorActionKind Kind,
    string Label
);
