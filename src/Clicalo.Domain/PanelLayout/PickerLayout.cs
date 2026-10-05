namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The profile grid (SEL-003): tiles of 76 high, at least 88 wide in Full and 80 in Compact, as many columns as fit
/// with a gap of 6 (<c>repeat(auto-fill, minmax(88px, 1fr))</c> of the prototype).
/// </summary>
public static class PickerLayout
{
    /// <summary>Gap between the tiles.</summary>
    public const int GapPx = 6;

    /// <summary>Smallest tile width in the Full view.</summary>
    public const int FullMinTileWidthPx = 88;

    /// <summary>Smallest tile width in the Compact view.</summary>
    public const int CompactMinTileWidthPx = 80;

    /// <summary>Columns that fit in <paramref name="widthPx"/>; at least 1.</summary>
    /// <param name="widthPx">The width of the grid.</param>
    /// <param name="compact">The Compact view.</param>
    public static int Columns(double widthPx, bool compact)
    {
        var min = compact ? CompactMinTileWidthPx : FullMinTileWidthPx;
        if (double.IsNaN(widthPx) || widthPx < min)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Floor((widthPx + GapPx) / (min + GapPx)));
    }
}
