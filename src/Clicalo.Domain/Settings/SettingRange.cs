namespace Clicalo.Domain.Settings;

/// <summary>
/// The allowed values of a numeric setting: an inclusive range and the step of its − / + controls. Used to clamp on
/// load (§6.5) and to convert v1 values (MIG-006: opacity 0.68 becomes 0.70).
/// </summary>
/// <param name="Min">Lowest value.</param>
/// <param name="Max">Highest value.</param>
/// <param name="Step">Increment of the − / + controls; also the grid of <see cref="Snap"/>, anchored at <paramref name="Min"/>.</param>
public sealed record SettingRange(double Min, double Max, double Step)
{
    /// <summary>Whether <paramref name="value"/> is inside the range.</summary>
    /// <param name="value">The value.</param>
    public bool Contains(double value) => value >= Min && value <= Max;

    /// <summary>The value brought inside the range.</summary>
    /// <param name="value">The value.</param>
    public double Clamp(double value) => Math.Clamp(value, Min, Max);

    /// <summary>
    /// The nearest value of the grid <c>Min + k·Step</c> inside the range (halves away from zero), rounded to 10
    /// decimals so binary fractions do not leak into the document.
    /// </summary>
    /// <param name="value">The value.</param>
    public double Snap(double value)
    {
        var steps = Math.Round((Clamp(value) - Min) / Step, MidpointRounding.AwayFromZero);
        return Math.Round(Clamp(Min + (steps * Step)), 10);
    }
}
