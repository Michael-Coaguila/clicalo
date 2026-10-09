using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// <see cref="DragTracker"/>: the grip, the title, the bubble and the handle drag once the contact passes
/// max(6, cancelMovePx); a gesture that became a drag is not a tap (PAN-004, PES-002, BUR-001).
/// </summary>
public sealed class DragTrackerTests
{
    private static readonly TouchSettings Standard = new(
        TimeSpan.Zero,
        45,
        8,
        TimeSpan.FromMilliseconds(150)
    );

    [Theory]
    [InlineData(0, 1.0, 6)]
    [InlineData(4, 1.0, 6)]
    [InlineData(8, 1.0, 8)]
    [InlineData(24, 1.0, 24)]
    [InlineData(8, 1.5, 12)]
    [Trait("Req", "PAN-004")]
    public void The_threshold_is_the_larger_of_6_and_the_cancel_distance(
        int cancel,
        double scale,
        double expected
    ) => DragTracker.Threshold(Standard with { CancelMovePx = cancel }, scale).ShouldBe(expected);

    [Fact]
    [Trait("Req", "PAN-004")]
    public void A_contact_that_lifts_before_the_threshold_is_a_tap()
    {
        var drag = new DragTracker(Standard, 1);

        drag.Down(1, new PhysicalPoint(100, 100)).ShouldBeTrue();
        drag.Move(1, new PhysicalPoint(105, 103)).ShouldBeNull();

        drag.Up(1).ShouldBe(DragEnd.Tapped);
        drag.IsTracking.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PAN-004")]
    public void Past_the_threshold_every_move_reports_its_offset_and_the_end_is_not_a_tap()
    {
        var drag = new DragTracker(Standard, 1);
        _ = drag.Down(1, new PhysicalPoint(100, 100));

        drag.Move(1, new PhysicalPoint(109, 100)).ShouldBe(new PhysicalOffset(9, 0));
        drag.IsDragging.ShouldBeTrue();
        drag.Move(1, new PhysicalPoint(102, 101)).ShouldBe(new PhysicalOffset(2, 1));

        drag.Up(1).ShouldBe(DragEnd.Dragged);
    }

    [Fact]
    [Trait("Req", "PAN-004")]
    public void Only_the_first_contact_drags()
    {
        var drag = new DragTracker(Standard, 1);
        _ = drag.Down(1, new PhysicalPoint(100, 100));

        drag.Down(2, new PhysicalPoint(300, 300)).ShouldBeFalse();
        drag.Move(2, new PhysicalPoint(400, 400)).ShouldBeNull();
        drag.Up(2).ShouldBe(DragEnd.None);
        drag.PointerId.ShouldBe(1u);
    }

    [Fact]
    [Trait("Req", "PES-002")]
    public void A_reset_forgets_the_contact()
    {
        var drag = new DragTracker(Standard, 1);
        _ = drag.Down(1, new PhysicalPoint(100, 100));
        _ = drag.Move(1, new PhysicalPoint(200, 100));

        drag.Reset();

        drag.IsTracking.ShouldBeFalse();
        drag.Up(1).ShouldBe(DragEnd.None);
    }
}
