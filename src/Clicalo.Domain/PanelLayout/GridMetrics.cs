using Clicalo.Domain.Catalog;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The measures of the shortcut grid (docs/04 «Medidas por tamaño» and §10; CUA-001, CUA-007, CUA-011, PAN-002), all
/// from <c>data/catalogs/sizes.json</c>. The rows that fit come from a <b>measured</b> space, never an estimate: the
/// surface measures how much height is left for the grid down to the bottom of the work area and passes it here.
/// </summary>
public static class GridMetrics
{
    /// <summary>
    /// Height of a tile: <c>h + (scale − 1) × label × 1.6</c>, rounded, and 16 px lower in Compact (CUA-007, CUA-011).
    /// </summary>
    /// <param name="settings">Size, view and text scale.</param>
    public static int TileHeight(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var layout = PanelSizes.Layout;
        var size = settings.Metrics;
        var scale = Math.Clamp(
            settings.TextScalePercent,
            (int)SettingsSchema.TextScalePercent.Min,
            (int)SettingsSchema.TextScalePercent.Max
        );
        var growth = (scale - 100) / 100d * size.TileLabelPx * layout.TextScaleTileGrowth;
        var height =
            size.TileHeightPx
            + growth
            - (settings.Compact ? layout.PanelCompactTileReductionPx : 0);
        return (int)Math.Round(height, MidpointRounding.AwayFromZero);
    }

    /// <summary>Width of the panel: <c>max(288, cols·w + (cols−1)·gap + 24)</c> (PAN-002).</summary>
    /// <param name="settings">Size and columns.</param>
    public static int PanelWidth(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var layout = PanelSizes.Layout;
        var size = settings.Metrics;
        var columns = ColumnsOf(settings);
        return Math.Max(
            layout.PanelMinWidthPx,
            (columns * size.TileWidthPx) + ((columns - 1) * size.GapPx) + layout.PanelChromeWidthPx
        );
    }

    /// <summary>
    /// The rows the preference allows: 1, 2 or 3; automatic (0) is 2 in S and 3 in M and L (CUA-001).
    /// </summary>
    /// <param name="settings">Size and row preference.</param>
    public static int MaxRows(PanelLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.RowsPreference <= 0
            ? settings.Metrics.AutoRows
            : Math.Min(settings.RowsPreference, PanelSizes.Layout.PanelMaxGridRows);
    }

    /// <summary>Whole rows that fit in <paramref name="gridSpacePx"/>: <c>⌊(space + gap) / (h + gap)⌋</c> (CUA-002).</summary>
    /// <param name="gridSpacePx">The measured height available to the grid.</param>
    /// <param name="tileHeightPx">Height of a tile.</param>
    /// <param name="gapPx">Gap between rows.</param>
    public static int RowsThatFit(double gridSpacePx, int tileHeightPx, int gapPx)
    {
        if (double.IsNaN(gridSpacePx) || gridSpacePx <= 0 || tileHeightPx <= 0)
        {
            return 0;
        }

        if (double.IsPositiveInfinity(gridSpacePx))
        {
            return int.MaxValue;
        }

        return (int)Math.Floor((gridSpacePx + gapPx) / (tileHeightPx + gapPx));
    }

    /// <summary>
    /// The shape of the grid: visible rows = <c>max(1, min(max rows, rows that fit))</c> (CUA-001). Without a
    /// measure yet (<see langword="null"/>), every allowed row.
    /// </summary>
    /// <param name="settings">The layout settings.</param>
    /// <param name="gridSpacePx">The measured height available to the grid, or <see langword="null"/>.</param>
    public static GridShape Shape(PanelLayoutSettings settings, double? gridSpacePx)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var tile = TileHeight(settings);
        var gap = settings.Metrics.GapPx;
        var max = MaxRows(settings);
        var rows = gridSpacePx is { } space
            ? Math.Max(1, Math.Min(max, RowsThatFit(space, tile, gap)))
            : max;
        return new GridShape(ColumnsOf(settings), rows, max, tile, gap);
    }

    /// <summary>Height of <paramref name="rows"/> rows of tiles and the gaps between them.</summary>
    /// <param name="rows">Rows.</param>
    /// <param name="tileHeightPx">Height of a tile.</param>
    /// <param name="gapPx">Gap between rows.</param>
    public static int HeightOf(int rows, int tileHeightPx, int gapPx) =>
        rows <= 0 ? 0 : (rows * tileHeightPx) + ((rows - 1) * gapPx);

    private static int ColumnsOf(PanelLayoutSettings settings) =>
        Math.Clamp(
            settings.Columns,
            (int)SettingsSchema.Columns.Min,
            (int)SettingsSchema.Columns.Max
        );
}
