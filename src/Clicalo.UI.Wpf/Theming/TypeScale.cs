using System.Globalization;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Settings;

namespace Clicalo.UI.Wpf.Theming;

/// <summary>
/// The type scale of the prototype (docs/07 «Tipografía»: 11 · 12 · 13 · 14 · 15 · 16 · 18 · 20 · 24 · 28 · 30 · 40
/// px) and the person's text scale (100 to 150 %, CUA-011). No text goes below
/// <see cref="Minimum"/> (11 px, TEM-007).
/// </summary>
public static class TypeScale
{
    /// <summary>The sizes of the scale, in device-independent pixels, from the smallest.</summary>
    public static IReadOnlyList<double> Steps { get; } =
    [11, 12, 13, 14, 15, 16, 18, 20, 24, 28, 30, 40];

    /// <summary>Smallest text size (TEM-007, <c>sizes.json</c> → <c>minTextPx</c>).</summary>
    public static double Minimum { get; } = PanelSizes.Layout.MinTextPx;

    /// <summary>Lowest text scale, in percent (<see cref="SettingsSchema.TextScalePercent"/>).</summary>
    public static int MinScalePercent { get; } = (int)SettingsSchema.TextScalePercent.Min;

    /// <summary>Highest text scale, in percent (<see cref="SettingsSchema.TextScalePercent"/>).</summary>
    public static int MaxScalePercent { get; } = (int)SettingsSchema.TextScalePercent.Max;

    /// <summary>
    /// A design size scaled by the person's text scale as the prototype does, <c>round(base × scale)</c> with halves
    /// rounded up, and raised to <see cref="Minimum"/> (CUA-011, TEM-007).
    /// </summary>
    /// <param name="designPx">Size in the design, in device-independent pixels.</param>
    /// <param name="textScalePercent">The text scale, from <see cref="MinScalePercent"/> to <see cref="MaxScalePercent"/>.</param>
    public static double Scale(double designPx, int textScalePercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(designPx);
        EnsureScale(textScalePercent);
        return Math.Max(
            Minimum,
            Math.Round(designPx * textScalePercent / 100d, MidpointRounding.AwayFromZero)
        );
    }

    /// <summary>Throws when <paramref name="textScalePercent"/> is outside the setting's range.</summary>
    internal static void EnsureScale(int textScalePercent)
    {
        if (textScalePercent < MinScalePercent || textScalePercent > MaxScalePercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(textScalePercent),
                textScalePercent,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The text scale goes from {MinScalePercent} to {MaxScalePercent} %."
                )
            );
        }
    }
}
