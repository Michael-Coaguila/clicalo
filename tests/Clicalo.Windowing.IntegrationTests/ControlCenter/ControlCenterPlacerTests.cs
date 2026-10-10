using Clicalo.Domain.Geometry;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// Where the Control Center opens (CCM-001, CCM-004, D9): beside the panel on either axis and on any monitor, with a
/// gap, and where it was left, with its size and its monitor. Pure geometry in physical pixels.
/// </summary>
public sealed class ControlCenterPlacerTests
{
    // 1920 × 1080 at 100 %, with a taskbar of 48 at the bottom.
    private static readonly DisplayMonitor Primary = new(
        @"\\.\DISPLAY1",
        new PhysicalRect(0, 0, 1920, 1080),
        new PhysicalRect(0, 0, 1920, 1032),
        IsPrimary: true,
        Scale: 1
    );

    // 2560 × 1440 at 150 %, to the right of the primary one.
    private static readonly DisplayMonitor Second = new(
        @"\\.\DISPLAY2",
        new PhysicalRect(1920, 0, 2560, 1440),
        new PhysicalRect(1920, 0, 2560, 1392),
        IsPrimary: false,
        Scale: 1.5
    );

    private static readonly DisplayMonitor[] Both = [Primary, Second];

    [Fact]
    [Trait("Req", "CCM-001")]
    public void The_first_time_it_opens_centered_with_the_default_size()
    {
        var spot = ControlCenterPlacer.Plan([Primary], null, null);

        spot.Monitor.ShouldBe(Primary);
        spot.Bounds.ShouldBe(new PhysicalRect(400, 176, 1120, 680));
        spot.Maximized.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    public void A_panel_on_the_right_edge_moves_it_to_the_left_with_a_gap()
    {
        // The panel: 316 wide at the right edge, in the middle of the height, where the centered window would be.
        var panel = new PhysicalRect(1300, 300, 316, 500);

        var spot = ControlCenterPlacer.Plan([Primary], panel, null);

        Overlaps(spot.Bounds, panel).ShouldBeFalse();
        spot.Bounds.Right.ShouldBe(panel.Left - 12, "a gap of 12 between them");
        spot.Bounds.Width.ShouldBe(1120);
        spot.Bounds.Height.ShouldBe(680);
        spot.Bounds.Top.ShouldBe(176, "it only moves on the axis that frees it");
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    public void A_wide_panel_across_the_middle_moves_it_on_the_vertical_axis()
    {
        // A wide strip (the bar of the tab, or a panel of many columns) across the screen: no room left or right.
        var panel = new PhysicalRect(200, 700, 1500, 120);

        var spot = ControlCenterPlacer.Plan([Primary], panel, null);

        Overlaps(spot.Bounds, panel).ShouldBeFalse();
        spot.Bounds.Bottom.ShouldBe(panel.Top - 12);
        spot.Bounds.Width.ShouldBe(1120, "the width stays");
        spot.Bounds.Height.ShouldBe(680);
        spot.Bounds.Left.ShouldBe(400);
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    [Trait("Req", "CCM-001")]
    public void With_little_room_it_shrinks_down_to_the_least_size_instead_of_covering_the_panel()
    {
        // The panel in the middle leaves 900 to its right: less than 1120, more than the least 760.
        var panel = new PhysicalRect(700, 100, 308, 600);

        var spot = ControlCenterPlacer.Plan([Primary], panel, null);

        Overlaps(spot.Bounds, panel).ShouldBeFalse();
        spot.Bounds.Left.ShouldBe(panel.Right + 12);
        spot.Bounds.Width.ShouldBe(1920 - (panel.Right + 12));
        spot.Bounds.Width.ShouldBeGreaterThanOrEqualTo(760);
        spot.Bounds.Height.ShouldBe(680);
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    public void It_opens_on_the_monitor_of_the_panel_and_avoids_it_there_with_that_monitor_scale()
    {
        // The panel on the second monitor (150 %), near its left edge.
        var panel = new PhysicalRect(2000, 300, 474, 750);

        var spot = ControlCenterPlacer.Plan(Both, panel, null);

        spot.Monitor.ShouldBe(Second);
        Overlaps(spot.Bounds, panel).ShouldBeFalse();
        spot.Bounds.Width.ShouldBe(1680, "1120 logical pixels at 150 %");
        spot.Bounds.Height.ShouldBe(1020);
        spot.Bounds.Left.ShouldBe(panel.Right + 18, "the gap of 12 at 150 %");
        Second
            .WorkArea.Contains(new PhysicalPoint(spot.Bounds.Left, spot.Bounds.Top))
            .ShouldBeTrue();
        spot.Bounds.Right.ShouldBeLessThanOrEqualTo(Second.WorkArea.Right);
    }

    [Fact]
    [Trait("Req", "CCM-001")]
    public void It_comes_back_with_the_size_the_place_and_the_monitor_it_was_left_with()
    {
        var left = new PhysicalRect(2300, 150, 1500, 900);
        var remembered = ControlCenterPlacer.Remember(Both, left, maximized: false);

        remembered.MonitorId.ShouldBe(Second.Id);
        remembered.Width.ShouldBe(1000, "logical pixels of that monitor");
        remembered.Height.ShouldBe(600);
        remembered.IsUsable.ShouldBeTrue();

        var spot = ControlCenterPlacer.Plan(Both, new PhysicalRect(100, 100, 316, 500), remembered);

        spot.Monitor.ShouldBe(Second, "its own monitor, even when the panel is on another one");
        spot.Bounds.Left.ShouldBeInRange(left.Left - 1, left.Left + 1);
        spot.Bounds.Top.ShouldBe(left.Top);
        spot.Bounds.Width.ShouldBe(1500);
        spot.Bounds.Height.ShouldBe(900);
    }

    [Fact]
    [Trait("Req", "CCM-001")]
    public void A_monitor_that_is_gone_a_tiny_size_and_a_maximized_window_are_handled()
    {
        var onSecond = new ControlCenterPlacement(Second.Id, 1400, 100, 300, 200, Maximized: true);

        // The second monitor is disconnected: default size on the primary one, not maximized.
        var fallback = ControlCenterPlacer.Plan([Primary], null, onSecond);
        fallback.Monitor.ShouldBe(Primary);
        fallback.Bounds.ShouldBe(new PhysicalRect(400, 176, 1120, 680));
        fallback.Maximized.ShouldBeFalse();

        // Connected: maximized there, and the restored size never under the least one.
        var spot = ControlCenterPlacer.Plan(Both, null, onSecond);
        spot.Monitor.ShouldBe(Second);
        spot.Maximized.ShouldBeTrue();
        spot.Bounds.Width.ShouldBe(1140, "760 logical pixels at 150 %");
        spot.Bounds.Height.ShouldBe(780);

        // An unusable value is as good as none.
        var broken = new ControlCenterPlacement(string.Empty, 0, 0, 0, 0, false);
        ControlCenterPlacer.Plan([Primary], null, broken).Bounds.Width.ShouldBe(1120);
    }

    [Fact]
    [Trait("Req", "CCM-001")]
    public void On_a_small_screen_the_least_size_is_the_work_area()
    {
        // 1366 × 768 at 150 %: 911 × 512 logical pixels, with a taskbar.
        var small = new DisplayMonitor(
            @"\\.\DISPLAY1",
            new PhysicalRect(0, 0, 1366, 768),
            new PhysicalRect(0, 0, 1366, 720),
            IsPrimary: true,
            Scale: 1.5
        );

        var spot = ControlCenterPlacer.Plan([small], null, null);

        spot.Bounds.ShouldBe(small.WorkArea, "it never opens larger than the work area");
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    public void The_rectangle_of_the_panel_window_is_read_with_the_scale_of_its_monitor()
    {
        // WPF reports a window on the 150 % monitor in units of that monitor.
        ControlCenterPlacer
            .FromWindowUnits(Both, 2000, 200, 316, 500)
            .ShouldBe(new PhysicalRect(3000, 300, 474, 750));
        ControlCenterPlacer
            .FromWindowUnits(Both, 100, 200, 316, 500)
            .ShouldBe(new PhysicalRect(100, 200, 316, 500));
        ControlCenterPlacer.FromWindowUnits(Both, 0, 0, 0, 0).ShouldBeNull("a hidden panel");
        ControlCenterPlacer.FromWindowUnits(Both, double.NaN, 0, 316, 500).ShouldBeNull();
    }

    private static bool Overlaps(PhysicalRect a, PhysicalRect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
}
