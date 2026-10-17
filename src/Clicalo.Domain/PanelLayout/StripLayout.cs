using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The Always visible row (FIJ-001 to FIJ-003): 4 columns; 4 shortcuts in S or Compact and 8 in M or L; with more,
/// capacity − 1 per page and a «··· i/N» chip that cycles. Direction C (D12) keeps the name on every tile, in every size
/// and view: a tile without a name was one of the causes of the critique, and the row of C is 64 high in every view.
/// </summary>
public static class StripLayout
{
    /// <summary>Columns of the row (FIJ-002).</summary>
    public const int Columns = 4;

    /// <summary>
    /// Height of the row in the Compact view: the large control height of direction C, as in Full (D12, FIJ-002), the
    /// least that holds the icon and the name of a tile at the largest text.
    /// </summary>
    public const int CompactHeightPx = 64;

    /// <summary>Shortcuts the row holds before paging: 4 in S or Compact, 8 in M or L (FIJ-003).</summary>
    /// <param name="settings">Size and view.</param>
    public static int Capacity(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Compact
            ? PanelSizes.Layout.PanelCompactStripCapacity
            : settings.Metrics.StripCapacity;
    }

    /// <summary>Height of a tile of the row: 64 in S, M and L, in Full and in Compact (FIJ-002, D12).</summary>
    /// <param name="settings">Size and view.</param>
    public static int TileHeight(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Compact ? CompactHeightPx : settings.Metrics.StripHeightPx;
    }

    /// <summary>
    /// Whether the tiles show their name: always in direction C (D12), which replaced the icons alone of S and Compact
    /// (FIJ-002) with a name in one line with «…».
    /// </summary>
    /// <param name="settings">Size and view.</param>
    public static bool ShowsNames(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return true;
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
