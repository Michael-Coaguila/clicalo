using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The Always visible row (FIJ-001 to FIJ-003): 4 columns; 4 shortcuts in S or Compact and 8 in M or L; with more,
/// capacity − 1 per page and a «··· i/N» chip that cycles. In S and in Compact the row is a row of icons as high as a
/// touch target (FIJ-002, VCO-001; D19): the name, in a quarter of the narrowest panel, ended in «…», and a row of the
/// height of M made S and Compact as tall as M. In M and L it keeps the name, 64 high.
/// </summary>
public static class StripLayout
{
    /// <summary>Columns of the row (FIJ-002).</summary>
    public const int Columns = 4;

    /// <summary>Height of the row in the Compact view: one touch target, for its icons (VCO-001, REG-02).</summary>
    public static int CompactHeightPx => PanelSizes.Layout.MinTouchTargetPx;

    /// <summary>Shortcuts the row holds before paging: 4 in S or Compact, 8 in M or L (FIJ-003).</summary>
    /// <param name="settings">Size and view.</param>
    public static int Capacity(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Compact
            ? PanelSizes.Layout.PanelCompactStripCapacity
            : settings.Metrics.StripCapacity;
    }

    /// <summary>Height of a tile of the row: the size's (44 in S, 64 in M and L) and 44 in Compact (FIJ-002, D19).</summary>
    /// <param name="settings">Size and view.</param>
    public static int TileHeight(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Compact ? CompactHeightPx : settings.Metrics.StripHeightPx;
    }

    /// <summary>
    /// Whether the tiles show their name: when the row is taller than one touch target, as in M and L. A row of the
    /// height of a touch target shows icons alone (S, Compact); the name stays for UI Automation and the voice
    /// numbers (FIJ-002, VCO-001).
    /// </summary>
    /// <param name="settings">Size and view.</param>
    public static bool ShowsNames(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return TileHeight(settings) > PanelSizes.Layout.MinTouchTargetPx;
    }

    /// <summary>The page <paramref name="page"/> of a row of <paramref name="count"/> shortcuts (FIJ-003).</summary>
    /// <param name="count">Shortcuts in the row.</param>
    /// <param name="capacity">Shortcuts the row holds before paging.</param>
    /// <param name="page">The page asked for; it cycles.</param>
    public static StripWindow Window(int count, int capacity, int page)
    {
        var total = Math.Max(0, count);
        var cap = Math.Max(2, capacity);
        var more = total > cap;
        var perPage = more ? cap - 1 : cap;
        var pages = more ? ((total - 1) / perPage) + 1 : 1;
        var current = ((page % pages) + pages) % pages;
        var start = current * perPage;
        return new StripWindow(current, pages, start, Math.Min(perPage, total - start), more);
    }
}
