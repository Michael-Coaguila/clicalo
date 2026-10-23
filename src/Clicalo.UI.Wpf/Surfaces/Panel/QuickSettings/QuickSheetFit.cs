namespace Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;

/// <summary>
/// The height of the Quick settings sheet in its window (AJR-001, D21): the room it has down to the foot of the work
/// area, never below its minimum, and its content when that is less.
/// </summary>
public static class QuickSheetFit
{
    /// <summary>The sheet never gets lower than this to fit the work area: the rest scrolls inside.</summary>
    public const double MinimumHeight = 120;

    /// <summary>The largest height the sheet may take in <paramref name="room"/>.</summary>
    /// <param name="room">The free height for the sheet, in DIPs.</param>
    public static double MaxHeight(double room) => Math.Max(MinimumHeight, room);

    /// <summary>
    /// The height the sheet will have with <paramref name="maxHeight"/>: its content when that is lower, or the whole of
    /// it when the content is not measured yet.
    /// </summary>
    /// <param name="maxHeight">The limit of <see cref="MaxHeight"/>.</param>
    /// <param name="natural">The height of the whole content of the sheet, when it was laid out once.</param>
    public static double Height(double maxHeight, double? natural) =>
        natural is { } content ? Math.Min(maxHeight, content) : maxHeight;
}
