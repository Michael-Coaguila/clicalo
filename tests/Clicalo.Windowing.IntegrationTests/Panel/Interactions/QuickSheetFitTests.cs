using Clicalo.Domain.PanelLayout;
using Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// AJR-001 and CUA-001 without a screen: the room of Quick settings and the rows of the grid come from one layout, so
/// a panel that opens the sheet reaches its height in one layout pass. The model is the column of the panel: what is
/// above the sheet, the sheet, what is under it and the grid. The old rule, kept here as <see cref="OldPass"/>, sized
/// the sheet with the height the grid still had on screen and the rows the grid had just been given: on the person's
/// screen the panel went 791 → 1600 → 1348 → 1628 → 1447 → 1474 px in a second (C:\dev\clicalo-qa\live).
/// </summary>
[Trait("Req", "AJR-001")]
[Trait("Req", "CUA-001")]
public sealed class QuickSheetFitTests
{
    // The panel S of the person's screen (2400 × 1600 at 175 %), in DIPs.
    private const double WorkBottom = 866;
    private const double BottomMargin = 16;
    private const double PanelTop = 120;
    private const double Above = 60;
    private const double SheetMargin = 10;
    private const double Under = 300;
    private const int Tile = 64;
    private const int Gap = 8;
    private const int MaxRows = 2;
    private const double ScreenLimit = 0.62 * 840;
    private const double SheetContent = 1100;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void The_room_of_the_sheet_does_not_depend_on_the_rows_the_grid_shows(int rows)
    {
        var below = SheetMargin + Under + Grid(rows);

        var room = QuickSheetFit.Room(Available(), below, Slack(rows));

        room.ShouldBe(Available() - SheetMargin - Under - Grid(1));
    }

    [Fact]
    public void The_sheet_is_never_above_62_percent_of_the_screen_nor_below_its_minimum()
    {
        QuickSheetFit.MaxHeight(2000, ScreenLimit).ShouldBe(ScreenLimit);
        QuickSheetFit.MaxHeight(10, ScreenLimit).ShouldBe(QuickSheetFit.MinimumHeight);
        QuickSheetFit.Height(400, natural: 300).ShouldBe(300);
        QuickSheetFit
            .Height(400, natural: null)
            .ShouldBe(400, "a sheet never laid out fills its room");
    }

    [Fact]
    public void Opening_the_sheet_reaches_its_height_in_one_pass_and_stays_inside_the_work_area()
    {
        // Opening: the panel decides the sheet and the grid before the first pass (PanelWindow.OnQuickSettingsChanged).
        var sheet = QuickSheetFit.Height(
            QuickSheetFit.MaxHeight(
                QuickSheetFit.Room(
                    Available(),
                    SheetMargin + Under + Grid(MaxRows),
                    Slack(MaxRows)
                ),
                ScreenLimit
            ),
            natural: null
        );
        var rows = RowsFor(sheet);
        var heights = new List<double> { Height(sheet, rows) };
        for (var pass = 0; pass < 5; pass++)
        {
            (sheet, rows) = NewPass(sheet, rows);
            heights.Add(Height(sheet, rows));
        }

        heights.Distinct().Count().ShouldBe(1, string.Join(" → ", heights));
        (PanelTop + heights[0]).ShouldBeLessThanOrEqualTo(WorkBottom - BottomMargin);
    }

    [Fact]
    public void The_old_rule_made_the_panel_jump_past_the_work_area_and_back()
    {
        // The sheet opens at 62 % of the screen over the two rows the grid had.
        double sheet = ScreenLimit;
        var rows = MaxRows;
        var heights = new List<double> { Height(sheet, rows) };
        for (var pass = 0; pass < 5; pass++)
        {
            (sheet, rows) = OldPass(sheet, rows);
            heights.Add(Height(sheet, rows));
        }

        heights.Distinct().Count().ShouldBeGreaterThan(2, string.Join(" → ", heights));
        heights.Max().ShouldBeGreaterThan(WorkBottom - BottomMargin - PanelTop);
    }

    /// <summary>The fixed rule of a layout pass: the sheet from this layout, then the grid with the sheet it will have.</summary>
    private static (double Sheet, int Rows) NewPass(double sheet, int rows)
    {
        var below = SheetMargin + Under + Grid(rows);
        var next = QuickSheetFit.Height(
            QuickSheetFit.MaxHeight(
                QuickSheetFit.Room(Available(), below, Slack(rows)),
                ScreenLimit
            ),
            SheetContent
        );
        return (next, RowsFor(next));
    }

    /// <summary>
    /// The rule before: the grid measured with the sheet on screen, then the sheet sized with the grid on screen less
    /// the slack of the rows just given to the grid.
    /// </summary>
    private static (double Sheet, int Rows) OldPass(double sheet, int rows)
    {
        var newRows = RowsFor(sheet);
        var below = SheetMargin + Under + Grid(rows);
        var room = Available() - below + Slack(newRows);
        var next = Math.Min(QuickSheetFit.MaxHeight(room, ScreenLimit), SheetContent);
        return (next, newRows);
    }

    private static double Available() => WorkBottom - BottomMargin - PanelTop - Above;

    private static int RowsFor(double sheet)
    {
        var space = Available() - sheet - SheetMargin - Under - Gap;
        return Math.Max(1, Math.Min(MaxRows, GridMetrics.RowsThatFit(space, Tile, Gap)));
    }

    private static double Grid(int rows) => rows * (Tile + Gap);

    private static double Slack(int rows) => Grid(rows) - Grid(1);

    private static double Height(double sheet, int rows) =>
        Above + sheet + SheetMargin + Under + Grid(rows);
}
