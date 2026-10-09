using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary>
/// <see cref="PanelGeometry"/>: the panel and the bubble stay whole inside the work area of their monitor, at the
/// position saved for it, and go to the primary one when their monitor is gone (PAN-002, PAN-006, BUR-001).
/// </summary>
public sealed class PanelGeometryTests
{
    private static readonly DisplayMonitor Primary = new(
        @"\\.\DISPLAY1",
        new PhysicalRect(0, 0, 1920, 1080),
        new PhysicalRect(0, 0, 1920, 1040),
        IsPrimary: true,
        Scale: 1
    );

    private static readonly DisplayMonitor Second = new(
        @"\\.\DISPLAY2",
        new PhysicalRect(1920, 0, 2560, 1440),
        new PhysicalRect(1920, 0, 2560, 1400),
        IsPrimary: false,
        Scale: 1.5
    );

    private static readonly IReadOnlyList<DisplayMonitor> Both = [Primary, Second];

    [Fact]
    [Trait("Req", "PAN-002")]
    public void The_first_position_is_top_right_72_from_the_edge_and_40_from_the_top()
    {
        var rect = PanelGeometry.Initial(316, 500, Primary);

        rect.ShouldBe(new PhysicalRect(1920 - 72 - 316, 40, 316, 500));
    }

    /// <summary>Positions out of the work area and where they end, with a margin of 8.</summary>
    public static TheoryData<int, int, int, int> Clamps =>
        new()
        {
            { -500, -500, 8, 8 },
            { 100, 100, 100, 100 },
            { 1900, 100, 1920 - 8 - 316, 100 },
            { 100, 900, 100, 1040 - 8 - 500 },
            { 5000, 5000, 1920 - 8 - 316, 1040 - 8 - 500 },
        };

    [Theory]
    [MemberData(nameof(Clamps))]
    [Trait("Req", "PAN-002")]
    [Trait("Req", "PAN-006")]
    public void A_surface_never_leaves_the_work_area(
        int left,
        int top,
        int expectedLeft,
        int expectedTop
    )
    {
        var rect = PanelGeometry.Clamp(new PhysicalRect(left, top, 316, 500), Primary);

        rect.ShouldBe(new PhysicalRect(expectedLeft, expectedTop, 316, 500));
    }

    [Fact]
    [Trait("Req", "PAN-002")]
    public void The_margin_scales_with_the_monitor()
    {
        var rect = PanelGeometry.Clamp(new PhysicalRect(0, 0, 400, 400), Second);

        rect.Left.ShouldBe(1920 + 12);
        rect.Top.ShouldBe(12);
    }

    [Fact]
    [Trait("Req", "PAN-006")]
    public void The_position_saved_for_the_monitor_is_used()
    {
        ValueList<MonitorPosition> saved = [new(Second.Id, 2500, 300), new(Primary.Id, 200, 100)];

        var placement = PanelGeometry.Place(316, 500, saved, Second.Id, Both);

        placement.Monitor.ShouldBe(Second);
        placement.Bounds.ShouldBe(new PhysicalRect(2500, 300, 316, 500));
    }

    [Fact]
    [Trait("Req", "PAN-006")]
    public void A_monitor_that_is_gone_gives_way_to_the_primary_one_whole_inside_it()
    {
        ValueList<MonitorPosition> saved = [new(Second.Id, 2500, 300)];

        var placement = PanelGeometry.Place(316, 500, saved, Second.Id, [Primary]);

        placement.Monitor.ShouldBe(Primary);
        placement.Bounds.ShouldBe(PanelGeometry.Initial(316, 500, Primary));
    }

    [Fact]
    [Trait("Req", "PAN-006")]
    public void At_start_the_primary_monitor_wins_when_it_has_a_position()
    {
        ValueList<MonitorPosition> saved = [new(Second.Id, 2500, 300), new(Primary.Id, 200, 100)];

        PanelGeometry.Place(316, 500, saved, null, Both).Monitor.ShouldBe(Primary);
        PanelGeometry
            .Place(316, 500, [new(Second.Id, 2500, 300)], null, Both)
            .Monitor.ShouldBe(Second);
    }

    [Fact]
    [Trait("Req", "PAN-006")]
    public void The_monitor_of_a_surface_is_the_one_under_its_center()
    {
        PanelGeometry.MonitorOf(new PhysicalRect(1800, 100, 316, 500), Both).ShouldBe(Second);
        PanelGeometry.MonitorOf(new PhysicalRect(100, 100, 316, 500), Both).ShouldBe(Primary);
        PanelGeometry.MonitorOf(new PhysicalRect(-9000, -9000, 10, 10), Both).ShouldBe(Primary);
    }
}
