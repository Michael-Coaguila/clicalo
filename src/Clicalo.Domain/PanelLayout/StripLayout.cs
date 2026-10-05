using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The Always visible row (FIJ-001 to FIJ-003): 4 columns; 4 shortcuts in S or Compact and 8 in M or L; with more,
/// capacity − 1 per page and a «··· i/N» chip that cycles; names hidden in S and Compact.
/// </summary>
public static class StripLayout
{
    /// <summary>Columns of the row (FIJ-002).</summary>
    public const int Columns = 4;

    /// <summary>Visual height of the row in the Compact view (FIJ-002; 44 as touch target).</summary>
    public const int CompactHeightPx = 40;

    /// <summary>Shortcuts the row holds before paging: 4 in S or Compact, 8 in M or L (FIJ-003).</summary>
    /// <param name="settings">Size and view.</param>
    public static int Capacity(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Compact
            ? PanelSizes.Layout.PanelCompactStripCapacity
            : settings.Metrics.StripCapacity;
    }

    /// <summary>Visual height of a tile of the row: 44/60/72 in S/M/L and 40 in Compact (FIJ-002).</summary>
    /// <param name="settings">Size and view.</param>
    public static int TileHeight(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Compact ? CompactHeightPx : settings.Metrics.StripHeightPx;
    }

    /// <summary>Whether the tiles show their name: not in S nor in Compact (FIJ-002).</summary>
    /// <param name="settings">Size and view.</param>
    public static bool ShowsNames(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return !settings.IsSmall && !settings.Compact;
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
