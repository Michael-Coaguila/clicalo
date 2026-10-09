namespace Clicalo.Domain.PanelLayout;

/// <summary>The shape of the shortcut grid on screen (CUA-001): columns, visible rows and tile height.</summary>
/// <param name="Columns">Tiles per row.</param>
/// <param name="Rows">Visible rows: never fewer than 1 nor more than <paramref name="MaxRows"/>.</param>
/// <param name="MaxRows">The rows the preference allows (automatic: 2 in S, 3 in M and L).</param>
/// <param name="TileHeightPx">Height of a tile with the text scale and the view applied (CUA-007, CUA-011).</param>
/// <param name="GapPx">Gap between tiles.</param>
public sealed record GridShape(int Columns, int Rows, int MaxRows, int TileHeightPx, int GapPx)
{
    /// <summary>Tiles per page: columns × rows (CUA-004).</summary>
    public int PerPage => Columns * Rows;

    /// <summary>Height of the visible rows and the gaps between them.</summary>
    public int HeightPx => GridMetrics.HeightOf(Rows, TileHeightPx, GapPx);
}
