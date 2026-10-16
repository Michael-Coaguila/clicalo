namespace Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;

/// <summary>
/// The height of the Quick settings sheet in the panel (AJR-001), from <b>one</b> rendered layout: what is left from
/// the top of the sheet down to the lowest edge the panel may reach, less what the window shows under the sheet with
/// the grid on one row, and never above 62 % of the screen. Everything comes from the same layout pass, the rows of
/// the grid included, so the answer does not depend on how many rows the grid shows now: the sheet and the grid never
/// chase each other (a sheet sized with the rows the grid is about to get and the height it still has made the panel
/// grow past the work area, shrink and grow again for a second).
/// </summary>
public static class QuickSheetFit
{
    /// <summary>The sheet never gets lower than this to fit the work area: the rest scrolls inside.</summary>
    public const double MinimumHeight = 120;

    /// <summary>The room for the sheet with the grid on one row.</summary>
    /// <param name="available">From the top of the sheet down to the lowest edge the panel may reach, in DIPs.</param>
    /// <param name="below">What the window shows under the sheet now, its margin and the grid included, in DIPs.</param>
    /// <param name="gridSlack">How much higher the rendered grid is than one row, in DIPs (zero without a grid).</param>
    public static double Room(double available, double below, double gridSlack) =>
        available - below + gridSlack;

    /// <summary>The largest height the sheet may take in <paramref name="room"/>.</summary>
    /// <param name="room">The free height for the sheet (<see cref="Room"/>), in DIPs.</param>
    /// <param name="screenLimit">62 % of the height of the work area, in DIPs.</param>
    public static double MaxHeight(double room, double screenLimit) =>
        Math.Min(screenLimit, Math.Max(MinimumHeight, room));

    /// <summary>
    /// The height the sheet will have with <paramref name="maxHeight"/>: its content when that is lower, or the whole of
    /// it when the content is not measured yet.
    /// </summary>
    /// <param name="maxHeight">The limit of <see cref="MaxHeight"/>.</param>
    /// <param name="natural">The height of the whole content of the sheet, when it was laid out once.</param>
    public static double Height(double maxHeight, double? natural) =>
        natural is { } content ? Math.Min(maxHeight, content) : maxHeight;
}
