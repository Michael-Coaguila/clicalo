using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// The long press of CUA-014: a finger that rests <c>Timings.Touch.LongPress</c> (600 ms) on a tile opens its menu
/// instead of executing; it is cancelled by moving past the drag threshold or leaving the tile, and never applies to
/// hold targets.
/// </summary>
[Trait("Req", "CUA-014")]
[Trait("Req", "NFR-020")]
public sealed class GestureRecognizerLongPressTests
{
    private static readonly double LongPressMs = Timings.Touch.LongPress.TotalMilliseconds;
    private static readonly TouchTarget Tile = TouchScript.Tile(1, 100, 100, 120, 120);

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void Resting_the_long_press_time_opens_the_menu_and_no_tap_follows(string preset)
    {
        var script = new TouchScript(TouchPresetSettings.Get(preset), Tile);

        script.Down(1, 160, 160, 0);
        script.Recognizer.NextDeadline.ShouldBe(TouchScript.At(LongPressMs));
        script.Tick(LongPressMs - 1);
        script.Events.ShouldBeEmpty();
        script.Tick(LongPressMs).Up(1, 2_000);

        var longPress = script.Single();
        longPress.Kind.ShouldBe(GestureKind.LongPress);
        longPress.Target.ShouldBe(new TouchTargetId(1));
        longPress.Timestamp.ShouldBe(TouchScript.At(LongPressMs));
    }

    [Fact]
    public void A_late_timer_still_reports_the_long_press_at_its_own_time()
    {
        var script = new TouchScript(TouchPresetSettings.Get("mild-tremor"), Tile);

        // No tick: the lift arrives after the deadline, so the long press comes first, dated at 600 ms.
        script.Down(1, 160, 160, 0).Up(1, 900);

        var longPress = script.Single();
        longPress.Kind.ShouldBe(GestureKind.LongPress);
        longPress.Timestamp.ShouldBe(TouchScript.At(LongPressMs));
    }

    [Fact]
    public void Lifting_just_before_the_long_press_time_is_a_tap()
    {
        var script = new TouchScript(TouchPresetSettings.Get("mild-tremor"), Tile);

        script.Down(1, 160, 160, 0).Up(1, LongPressMs - 1).Tick(LongPressMs);

        script.Single().Kind.ShouldBe(GestureKind.Tap);
        script.Recognizer.NextDeadline.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    [Trait("Req", "PAN-004")]
    public void Moving_past_the_drag_threshold_cancels_the_long_press(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Tile);
        var threshold = Math.Max(Timings.Touch.DragMinDistancePx, settings.CancelMovePx);

        // At the threshold the long press survives; one pixel further it is gone for good.
        script.Down(1, 160, 110, 0).Move(1, 160, 110 + threshold, 100).Tick(LongPressMs);
        script.Kinds.ShouldBe([GestureKind.LongPress]);

        script.ClearEvents().Up(1, 1_000);
        script.Down(2, 160, 110, 2_000).Move(2, 160, 110 + threshold + 1, 2_100);
        script.Move(2, 160, 110, 2_200).Tick(2_000 + LongPressMs).Up(2, 2_000 + LongPressMs + 100);

        script.Events.ShouldNotContain(e => e.Kind == GestureKind.LongPress);
        script
            .Single()
            .Kind.ShouldBe(
                settings.CancelMovePx > 0 ? GestureKind.Ignored : GestureKind.Tap,
                "without a long press the lift is judged as a tap"
            );
    }

    [Fact]
    public void Leaving_the_tile_cancels_the_long_press()
    {
        var script = new TouchScript(TouchPresetSettings.PersonalSettings, Tile); // extra area 20, cancel off

        script.Down(1, 215, 160, 0).Move(1, 241, 160, 50).Move(1, 215, 160, 100).Tick(1_000);
        script.Up(1, 1_000);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.Moved);
    }

    [Fact]
    [Trait("Req", "EJE-004")]
    public void A_hold_target_never_long_presses()
    {
        var hold = TouchScript.Tile(1, 100, 100, kind: TouchTargetKind.Hold);
        var script = new TouchScript(TouchPresetSettings.Get("standard"), hold);

        script.Down(1, 140, 140, 0).Tick(5_000).Up(1, 5_000);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
    }
}
