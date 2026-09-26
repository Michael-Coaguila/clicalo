using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// Page swipes (CUA-005): more than <c>Timings.Touch.SwipeMinDistancePx</c> (60 logical px) horizontally with
/// |dy| &lt; 0.6·|dx|; the swipe never executes or opens the tile it started on, and tiles ignore touches for
/// <c>Timings.Touch.PostSwipeLock</c> (300 ms) afterwards.
/// </summary>
[Trait("Req", "CUA-005")]
[Trait("Req", "NFR-020")]
public sealed class GestureRecognizerSwipeTests
{
    private static readonly TouchTarget[] Grid =
    [
        TouchScript.Tile(1, 100, 100, 100, 100),
        TouchScript.Tile(2, 210, 100, 100, 100),
        TouchScript.Tile(3, 320, 100, 100, 100),
    ];

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void A_horizontal_move_of_more_than_the_swipe_distance_is_a_swipe(double dpi)
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), dpi, Grid);
        var distance = (int)Math.Floor(Timings.Touch.SwipeMinDistancePx * dpi) + 1;

        script
            .Down(1, 380, 150, 0)
            .Move(1, 380 - (distance / 2), 150, 40)
            .Up(1, 380 - distance, 150, 80);
        script.Down(2, 120, 150, 1_000).Up(2, 120 + distance, 150, 1_080);

        script.Kinds.ShouldBe([GestureKind.Swipe, GestureKind.Swipe]);
        script.Events[0].Swipe.ShouldBe(SwipeDirection.Left);
        script.Events[0].Target.ShouldBeNull();
        script.Events[1].Swipe.ShouldBe(SwipeDirection.Right);
    }

    [Fact]
    public void Exactly_the_swipe_distance_is_not_a_swipe()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), Grid);

        script.Down(1, 380, 150, 0).Up(1, 380 - Timings.Touch.SwipeMinDistancePx, 150, 80);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.Moved);
        ignored.Target.ShouldBe(new TouchTargetId(3));
    }

    [Theory]
    [InlineData(59, GestureKind.Swipe)]
    [InlineData(60, GestureKind.Ignored)]
    public void A_swipe_must_be_flatter_than_the_maximum_slope(int dy, GestureKind expected)
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), Grid);

        // dx = 100: |dy| must stay below 0.6 × 100.
        script.Down(1, 400, 120, 0).Up(1, 300, 120 + dy, 80);

        script.Single().Kind.ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void A_swipe_that_starts_on_a_tile_never_taps_or_opens_it(string preset)
    {
        var script = new TouchScript(TouchPresetSettings.Get(preset), Grid);

        // Slow: the finger rests 200 ms, then travels for another 700 ms, past the long press time.
        script.Down(1, 150, 150, 0).Move(1, 150, 152, 200).Move(1, 200, 150, 500);
        script.Tick(700).Move(1, 250, 150, 800).Up(1, 900);

        script.Single().Kind.ShouldBe(GestureKind.Swipe);
    }

    [Fact]
    [Trait("Req", "EJE-004")]
    public void A_swipe_that_starts_on_a_hold_releases_it_when_it_leaves_and_then_swipes()
    {
        TouchTarget[] layout =
        [
            TouchScript.Tile(1, 100, 100, 100, 100, TouchTargetKind.Hold),
            TouchScript.Tile(2, 210, 100, 100, 100),
        ];
        var script = new TouchScript(TouchPresetSettings.Get("mild-tremor"), layout);

        script.Down(1, 150, 150, 0).Move(1, 230, 150, 60).Up(1, 260, 150, 100);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd, GestureKind.Swipe]);
        script.Events[1].HoldEnd.ShouldBe(HoldEndReason.LeftTarget);
        script.Events[2].Swipe.ShouldBe(SwipeDirection.Right);
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void Tiles_ignore_touches_for_the_lock_after_a_swipe(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Grid);
        var lockMs = Timings.Touch.PostSwipeLock.TotalMilliseconds;
        var duration = Math.Max(40, settings.MinContact.TotalMilliseconds);

        script.Down(1, 380, 150, 0).Up(1, 280, 150, 100);
        script.Recognizer.NextDeadline.ShouldBe(TouchScript.At(100 + lockMs));
        // Two fingers: one goes down 1 ms before the lock ends, the other just as it ends.
        script.Down(2, 150, 150, 100 + lockMs - 1).Down(3, 260, 150, 100 + lockMs);
        script.Up(2, 100 + lockMs - 1 + duration).Up(3, 100 + lockMs + duration);

        script.Kinds.ShouldBe([GestureKind.Swipe, GestureKind.Ignored, GestureKind.Tap]);
        script.Events[1].Ignored.ShouldBe(IgnoreReason.SwipeLock);
        script.Events[1].Target.ShouldBe(new TouchTargetId(1));
        script.Events[2].Target.ShouldBe(new TouchTargetId(2));
    }

    [Fact]
    public void A_second_swipe_during_the_lock_still_turns_the_page()
    {
        var script = new TouchScript(TouchPresetSettings.Get("mild-tremor"), Grid);

        script.Down(1, 380, 150, 0).Up(1, 280, 150, 100);
        script.Down(2, 380, 150, 150).Up(2, 280, 150, 250);

        script.Kinds.ShouldBe([GestureKind.Swipe, GestureKind.Swipe]);
    }

    [Fact]
    public void A_swipe_that_starts_between_tiles_turns_the_page()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), Grid);

        script.Down(1, 250, 400, 0).Up(1, 150, 400, 100);

        script.Single().Kind.ShouldBe(GestureKind.Swipe);
    }
}
