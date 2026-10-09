namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>
/// A slider of «Precisión táctil» (TAC-005): name, value in accent monospace ([off] for 0), explanation and − / + of
/// 44. A preset value off the step (Temblor fuerte's 28) is kept exact; − and + round it to the nearest step.
/// </summary>
/// <param name="Value">Which value it edits.</param>
/// <param name="Label">[sDeb], [sHit], [sMov] or [sMin].</param>
/// <param name="Description">[sDebD], [sHitD], [sMovD] or [sMinD].</param>
/// <param name="Current">The value, in milliseconds or logical pixels.</param>
/// <param name="Display">«300 ms», «14 px» or [off].</param>
/// <param name="Minimum">The lowest value.</param>
/// <param name="Maximum">The highest value.</param>
/// <param name="Step">The step of − / + and of the track.</param>
/// <param name="LessName">The accessible name of −.</param>
/// <param name="MoreName">The accessible name of +.</param>
public sealed record TouchSlider(
    TouchValue Value,
    string Label,
    string Description,
    double Current,
    string Display,
    double Minimum,
    double Maximum,
    double Step,
    string LessName,
    string MoreName
);
