using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using SizeId = Clicalo.Domain.Catalog.PanelSize;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary>
/// <see cref="DockGeometry"/>: the handle, the bar, the windows beside it and the floating «Release all» of the Tab view,
/// inside the work area (docs/04 «Vista pestaña»).
/// </summary>
public sealed class DockGeometryTests
{
    private static readonly DisplayMonitor Monitor = new(
        @"\\.\DISPLAY1",
        new PhysicalRect(0, 0, 1920, 1080),
        new PhysicalRect(0, 0, 1920, 1040),
        IsPrimary: true,
        Scale: 1
    );

    /// <summary>
    /// Every edge, the window of its handle and where it stands at 50 %: 116 or 128 long and 44 deep, the touch target
    /// of the handle drawn 32 deep against the edge (REG-02, ACC-002).
    /// </summary>
    public static TheoryData<DockSide, PhysicalRect> Handles =>
        new()
        {
            { DockSide.Right, new PhysicalRect(1920 - 44, 520 - 58, 44, 116) },
            { DockSide.Left, new PhysicalRect(0, 520 - 58, 44, 116) },
            { DockSide.Top, new PhysicalRect(960 - 64, 0, 128, 44) },
            { DockSide.Bottom, new PhysicalRect(960 - 64, 1040 - 44, 128, 44) },
        };

    [Theory]
    [MemberData(nameof(Handles))]
    [Trait("Req", "PES-001")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "ACC-002")]
    public void The_handle_takes_a_touch_44_deep_along_116_on_a_side_and_128_on_top_and_bottom(
        DockSide side,
        PhysicalRect expected
    ) => DockGeometry.Handle(side, 50, Monitor, gutter: false).ShouldBe(expected);

    [Fact]
    [Trait("Req", "PES-001")]
    public void With_the_gutter_the_right_handle_leaves_18_px_for_the_scroll_bar()
    {
        DockGeometry
            .Handle(DockSide.Right, 50, Monitor, gutter: true)
            .Left.ShouldBe(1920 - 18 - 44);
        DockGeometry.Handle(DockSide.Left, 50, Monitor, gutter: true).Left.ShouldBe(0);
    }

    [Theory]
    [InlineData(DockSide.Right, 1919, 500, false)]
    [InlineData(DockSide.Right, 1918, 500, true)]
    [InlineData(DockSide.Left, 0, 500, false)]
    [InlineData(DockSide.Left, 1, 500, true)]
    [InlineData(DockSide.Top, 960, 0, false)]
    [InlineData(DockSide.Top, 960, 1, true)]
    [InlineData(DockSide.Bottom, 960, 1039, false)]
    [InlineData(DockSide.Bottom, 960, 1038, true)]
    [Trait("Req", "PES-002")]
    public void A_drag_of_the_handle_never_starts_on_the_pixel_of_the_screen_edge(
        DockSide side,
        int x,
        int y,
        bool starts
    ) =>
        DockGeometry
            .HandleDragStartsAt(
                side,
                DockGeometry.Handle(side, 50, Monitor, gutter: false),
                new PhysicalPoint(x, y)
            )
            .ShouldBe(starts);

    [Theory]
    [InlineData(0, 8)]
    [InlineData(8, 8)]
    [InlineData(50, 50)]
    [InlineData(92, 92)]
    [InlineData(100, 92)]
    [Trait("Req", "PES-001")]
    public void The_handle_position_stays_between_8_and_92_percent(int percent, int expected) =>
        DockGeometry.ClampPercent(percent).ShouldBe(expected);

    [Fact]
    [Trait("Req", "PES-002")]
    public void A_drag_moves_the_handle_along_its_edge_only()
    {
        // 104 px down on a 1040 px edge is 10 %; the drag across the edge does not count.
        DockGeometry
            .PercentAfterDrag(DockSide.Right, 50, new PhysicalOffset(-300, 104), Monitor)
            .ShouldBe(60);
        DockGeometry
            .PercentAfterDrag(DockSide.Bottom, 50, new PhysicalOffset(-192, -500), Monitor)
            .ShouldBe(40);
        DockGeometry
            .PercentAfterDrag(DockSide.Left, 50, new PhysicalOffset(0, 5000), Monitor)
            .ShouldBe(92);
    }

    [Theory]
    [InlineData(SizeId.S, 76, 58)]
    [InlineData(SizeId.M, 88, 66)]
    [InlineData(SizeId.L, 108, 78)]
    [Trait("Req", "PES-005")]
    public void The_bar_is_as_thick_as_its_size_says(SizeId size, int width, int height)
    {
        var metrics = PanelSizes.Get(size);

        DockGeometry
            .Bar(DockSide.Right, 400, metrics, Monitor, gutter: false)
            .Width.ShouldBe(width);
        DockGeometry
            .Bar(DockSide.Bottom, 400, metrics, Monitor, gutter: false)
            .Height.ShouldBe(height);
    }

    [Fact]
    [Trait("Req", "PES-005")]
    public void The_bar_is_centered_and_never_longer_than_the_work_area_less_24_or_40()
    {
        var metrics = PanelSizes.Get(SizeId.M);

        var vertical = DockGeometry.Bar(DockSide.Right, 5000, metrics, Monitor, gutter: false);
        vertical.ShouldBe(new PhysicalRect(1920 - 88, 12, 88, 1040 - 24));

        var horizontal = DockGeometry.Bar(DockSide.Bottom, 600, metrics, Monitor, gutter: false);
        horizontal.ShouldBe(new PhysicalRect(660, 1040 - 12 - 66, 600, 66));
        DockGeometry.Bar(DockSide.Top, 600, metrics, Monitor, gutter: false).Top.ShouldBe(12);
        DockGeometry.MaxBarLength(DockSide.Top, Monitor).ShouldBe(1920 - 40);
    }

    [Fact]
    [Trait("Req", "PES-010")]
    [Trait("Req", "PES-011")]
    public void A_window_beside_the_bar_goes_toward_the_screen_aligned_as_asked()
    {
        var button = new PhysicalRect(1840, 700, 80, 38);

        DockGeometry
            .Beside(DockSide.Right, button, 230, 300, 16, DockAlign.End, Monitor)
            .ShouldBe(new PhysicalRect(1840 - 16 - 230, 738 - 300, 230, 300));
        DockGeometry
            .Beside(
                DockSide.Left,
                new PhysicalRect(0, 200, 88, 600),
                236,
                300,
                8,
                DockAlign.Start,
                Monitor
            )
            .ShouldBe(new PhysicalRect(96, 200, 236, 300));
        DockGeometry
            .Beside(
                DockSide.Bottom,
                new PhysicalRect(600, 960, 700, 66),
                260,
                200,
                8,
                DockAlign.Start,
                Monitor
            )
            .ShouldBe(new PhysicalRect(600, 960 - 8 - 200, 260, 200));
    }

    [Fact]
    [Trait("Req", "PES-010")]
    public void A_window_beside_the_bar_stays_inside_the_work_area()
    {
        var rect = DockGeometry.Beside(
            DockSide.Right,
            new PhysicalRect(1840, 0, 80, 38),
            230,
            300,
            16,
            DockAlign.End,
            Monitor
        );

        rect.Top.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "PES-014")]
    public void The_notice_surface_goes_beside_the_bar_at_its_end_when_nothing_is_there()
    {
        var bar = new PhysicalRect(1832, 100, 88, 840);

        DockGeometry
            .BesideClear(DockSide.Right, bar, 300, 60, 8, DockAlign.End, Monitor, [])
            .ShouldBe(new PhysicalRect(1832 - 8 - 300, 940 - 60, 300, 60));

        // A window that is elsewhere beside the bar does not move it.
        DockGeometry
            .BesideClear(
                DockSide.Right,
                bar,
                300,
                60,
                8,
                DockAlign.End,
                Monitor,
                [new PhysicalRect(1588, 100, 236, 400)]
            )
            .ShouldBe(new PhysicalRect(1832 - 8 - 300, 940 - 60, 300, 60));
    }

    [Fact]
    [Trait("Req", "PES-014")]
    public void The_notice_surface_goes_further_in_when_a_window_or_Release_all_is_in_its_place()
    {
        var bar = new PhysicalRect(1832, 100, 88, 840);
        var pinned = new PhysicalRect(1832 - 16 - 230, 500, 230, 300);
        var pill = new PhysicalRect(1832 - 8 - 140 - 250, 800, 140, 44);

        var beyondPinned = DockGeometry.BesideClear(
            DockSide.Right,
            bar,
            300,
            400,
            8,
            DockAlign.End,
            Monitor,
            [pinned]
        );
        beyondPinned.Right.ShouldBe(pinned.Left - 8);
        beyondPinned.Bottom.ShouldBe(bar.Bottom);

        // Two in the way: beyond both.
        var beyondBoth = DockGeometry.BesideClear(
            DockSide.Right,
            bar,
            300,
            400,
            8,
            DockAlign.End,
            Monitor,
            [pinned, pill]
        );
        beyondBoth.Right.ShouldBe(Math.Min(pinned.Left, pill.Left) - 8);

        // Beside the closed handle with «Release all» centered beside it: the notice goes beyond the pill.
        var handle = new PhysicalRect(1920 - 44, 462, 44, 116);
        var besideHandle = DockGeometry.Beside(
            DockSide.Right,
            handle,
            140,
            44,
            8,
            DockAlign.Center,
            Monitor
        );
        var notice = DockGeometry.BesideClear(
            DockSide.Right,
            handle,
            300,
            60,
            8,
            DockAlign.End,
            Monitor,
            [besideHandle]
        );
        notice.Right.ShouldBe(besideHandle.Left - 8);
    }

    [Fact]
    [Trait("Req", "PES-013")]
    public void Release_all_floats_110_from_the_edge_or_180_from_the_bottom()
    {
        DockGeometry
            .Panic(DockSide.Right, 140, 44, Monitor)
            .ShouldBe(new PhysicalRect(1920 - 110 - 140, 498, 140, 44));
        DockGeometry.Panic(DockSide.Left, 140, 44, Monitor).Left.ShouldBe(110);
        DockGeometry.Panic(DockSide.Top, 140, 44, Monitor).Top.ShouldBe(110);
        DockGeometry.Panic(DockSide.Bottom, 140, 44, Monitor).Top.ShouldBe(1040 - 180 - 44);
    }

    [Theory]
    [InlineData(5, 1000, 58, 6, 5)]
    [InlineData(8, 200, 58, 6, 3)]
    [InlineData(4, 10, 58, 6, 1)]
    [InlineData(6, 186, 58, 6, 3)]
    [InlineData(6, 185, 58, 6, 2)]
    [Trait("Req", "PES-007")]
    public void Each_page_holds_the_preference_or_the_shortcuts_that_fit_whole(
        int preference,
        double space,
        double tile,
        double gap,
        int expected
    ) => DockGeometry.TilesPerPage(preference, space, tile, gap).ShouldBe(expected);
}
