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
            { PanelSize.Small, 100, false, 72 },
            { PanelSize.Medium, 100, false, 80 },
            { PanelSize.Large, 100, false, 96 },
            { PanelSize.Small, 150, false, 83 },
            { PanelSize.Medium, 150, false, 91 },
            { PanelSize.Large, 120, false, 101 },
            { PanelSize.Large, 150, false, 109 },
            { PanelSize.Small, 100, true, 56 },
            { PanelSize.Medium, 100, true, 64 },
            { PanelSize.Large, 150, true, 93 },
        };

    /// <summary>Size, columns, expected panel width.</summary>
    public static TheoryData<PanelSize, int, int> Widths =>
        new()
        {
            { PanelSize.Small, 2, 288 },
            { PanelSize.Small, 3, 292 },
            { PanelSize.Small, 4, 384 },
            { PanelSize.Medium, 3, 340 },
            { PanelSize.Medium, 4, 448 },
            { PanelSize.Large, 3, 412 },
            { PanelSize.Large, 4, 544 },
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
            { PanelSize.Medium, 0, 168, 2 },
            { PanelSize.Medium, 0, 167, 1 },
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

        shape.HeightPx.ShouldBe((3 * 80) + (2 * 8));
        GridMetrics.HeightOf(0, 80, 8).ShouldBe(0);
    }

    /// <summary>Text scale, chosen columns, whether the words fit, expected columns (D17).</summary>
    public static TheoryData<int, int, bool, int> ColumnsForWords =>
        new()
        {
            { 150, 3, false, 2 },
            { 120, 4, false, 2 },
            { 150, 3, true, 3 },
            { 100, 3, false, 3 },
            { 150, 2, false, 2 },
        };

    [Theory]
    [MemberData(nameof(ColumnsForWords))]
    [Trait("Req", "CUA-011")]
    [Trait("Req", "PAN-002")]
    public void A_word_that_does_not_fit_at_a_larger_text_gives_the_grid_two_columns_and_the_panel_keeps_its_size(
        int textScale,
        int columns,
        bool wordsFit,
        int expected
    )
    {
        var settings = Settings(PanelSize.Medium) with
        {
            TextScalePercent = textScale,
            Columns = columns,
        };

        var shape = GridMetrics.Shape(settings, null, wordsFit);

        shape.Columns.ShouldBe(expected);
        shape.PerPage.ShouldBe(expected * 3);
        GridMetrics.NarrowsForWords(settings).ShouldBe(textScale > 100 && columns > 2);

        // D16: the panel keeps the width of the chosen columns and the height of its rows.
        GridMetrics
            .PanelWidth(settings)
            .ShouldBe(GridMetrics.PanelWidth(settings with { TextScalePercent = 100 }));
        shape.HeightPx.ShouldBe(GridMetrics.Shape(settings, null).HeightPx);
    }

    private static PanelLayoutSettings Settings(PanelSize size) =>
        PanelLayoutSettings.Default with
        {
            Size = size,
        };
}
