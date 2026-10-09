using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary>
/// <see cref="GridMetrics"/> against the table of docs/04 «Medidas por tamaño» (S/M/L × 100–150 % × Full and
/// Compact) and simulated measures of the space left for the grid (CUA-001 «Acepta»).
/// </summary>
public sealed class GridMetricsTests
{
    /// <summary>Size, text scale, Compact, expected tile height.</summary>
    public static TheoryData<PanelSize, int, bool, int> TileHeights =>
        new()
        {
            { PanelSize.Small, 100, false, 66 },
            { PanelSize.Medium, 100, false, 78 },
            { PanelSize.Large, 100, false, 98 },
            { PanelSize.Small, 150, false, 76 },
            { PanelSize.Medium, 150, false, 89 },
            { PanelSize.Large, 120, false, 103 },
            { PanelSize.Large, 150, false, 111 },
            { PanelSize.Small, 100, true, 50 },
            { PanelSize.Medium, 100, true, 62 },
            { PanelSize.Large, 150, true, 95 },
        };

    /// <summary>Size, columns, expected panel width.</summary>
    public static TheoryData<PanelSize, int, int> Widths =>
        new()
        {
            { PanelSize.Small, 2, 288 },
            { PanelSize.Small, 3, 288 },
            { PanelSize.Small, 4, 330 },
            { PanelSize.Medium, 3, 316 },
            { PanelSize.Medium, 4, 416 },
            { PanelSize.Large, 3, 392 },
            { PanelSize.Large, 4, 518 },
        };

    /// <summary>Size, row preference, expected maximum rows.</summary>
    public static TheoryData<PanelSize, int, int> MaxRows =>
        new()
        {
            { PanelSize.Small, 0, 2 },
            { PanelSize.Medium, 0, 3 },
            { PanelSize.Large, 0, 3 },
            { PanelSize.Small, 3, 3 },
            { PanelSize.Medium, 1, 1 },
            { PanelSize.Large, 2, 2 },
        };

    /// <summary>Measured space, tile height, gap, expected whole rows.</summary>
    public static TheoryData<double, int, int, int> Fits =>
        new()
        {
            { 250, 78, 8, 3 },
            { 249, 78, 8, 2 },
            { 164, 78, 8, 2 },
            { 163, 78, 8, 1 },
            { 78, 78, 8, 1 },
            { 77, 78, 8, 0 },
            { 0, 78, 8, 0 },
            { -40, 78, 8, 0 },
        };

    /// <summary>Size, row preference, measured space (negative: not measured yet), expected visible rows.</summary>
    public static TheoryData<PanelSize, int, double, int> VisibleRows =>
        new()
        {
            { PanelSize.Medium, 0, -1, 3 },
            { PanelSize.Medium, 0, 10_000, 3 },
            { PanelSize.Medium, 0, 165, 2 },
            { PanelSize.Medium, 0, 10, 1 },
            { PanelSize.Small, 0, 10_000, 2 },
            { PanelSize.Small, 3, 10_000, 3 },
            { PanelSize.Large, 1, 10_000, 1 },
            { PanelSize.Large, 0, 206, 2 },
        };

    [Theory]
    [MemberData(nameof(TileHeights))]
    [Trait("Req", "CUA-007")]
    [Trait("Req", "CUA-011")]
    public void A_tile_grows_with_the_text_scale_and_is_16_lower_in_Compact(
        PanelSize size,
        int scale,
        bool compact,
        int expected
    ) =>
        GridMetrics
            .TileHeight(Settings(size) with { TextScalePercent = scale, Compact = compact })
            .ShouldBe(expected);

    [Theory]
    [MemberData(nameof(Widths))]
    [Trait("Req", "PAN-002")]
    public void The_panel_is_as_wide_as_its_columns_and_never_below_288(
        PanelSize size,
        int columns,
        int expected
    ) => GridMetrics.PanelWidth(Settings(size) with { Columns = columns }).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(MaxRows))]
    [Trait("Req", "CUA-001")]
    public void Automatic_rows_are_two_in_S_and_three_in_M_and_L(
        PanelSize size,
        int preference,
        int expected
    ) =>
        GridMetrics.MaxRows(Settings(size) with { RowsPreference = preference }).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(Fits))]
    [Trait("Req", "CUA-002")]
    public void Only_whole_rows_fit(double space, int tile, int gap, int expected) =>
        GridMetrics.RowsThatFit(space, tile, gap).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(VisibleRows))]
    [Trait("Req", "CUA-001")]
    public void Visible_rows_are_the_measured_ones_between_one_and_the_maximum(
        PanelSize size,
        int preference,
        double space,
        int expected
    )
    {
        var shape = GridMetrics.Shape(
            Settings(size) with
            {
                RowsPreference = preference,
            },
            space < 0 ? null : space
        );

        shape.Rows.ShouldBe(expected);
        shape.PerPage.ShouldBe(3 * expected);
    }

    [Fact]
    [Trait("Req", "CUA-001")]
    public void The_height_of_the_grid_counts_the_gaps_between_rows_only()
    {
        var shape = GridMetrics.Shape(Settings(PanelSize.Medium), null);

        shape.HeightPx.ShouldBe((3 * 78) + (2 * 8));
        GridMetrics.HeightOf(0, 78, 8).ShouldBe(0);
    }

    private static PanelLayoutSettings Settings(PanelSize size) =>
        PanelLayoutSettings.Default with
        {
            Size = size,
        };
}
