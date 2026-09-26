using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// Taps through the recognizer for the four configurations of TAC-001: the filter of TAC-002 per target, the extra
/// hit area (REG-02) and the reasons of ignored touches (TAC-003), at 100 % and 150 % scale.
/// </summary>
[Trait("Req", "TAC-001")]
[Trait("Req", "TAC-002")]
public sealed class GestureRecognizerTapTests
{
    // A tall tile, so a vertical move up to the largest cancel distance (80) stays on it: [100, 260) × [100, 260).
    private static readonly TouchTarget Tall = TouchScript.Tile(1, 100, 100, 160, 160);
    private static readonly TouchTarget Other = TouchScript.Tile(2, 400, 100, 160, 160);

    public static TheoryData<string, double> PresetsAndScales
    {
        get
        {
            var data = new TheoryData<string, double>();
            foreach (var preset in TouchPresetSettings.Ids)
            {
                data.Add(preset, 1.0);
                data.Add(preset, 1.5);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PresetsAndScales))]
    public void A_tap_on_a_target_is_reported_with_its_target_and_position(
        string preset,
        double dpi
    )
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, dpi, Tall, Other);

        script.Tap(1, 180, 180, atMs: 0, durationMs: Duration(settings));

        var tap = script.Single();
        tap.Kind.ShouldBe(GestureKind.Tap);
        tap.Target.ShouldBe(new TouchTargetId(1));
        tap.PointerId.ShouldBe(1u);
        tap.Position.ShouldBe(new PhysicalPoint(180, 180));
        tap.Timestamp.ShouldBe(TouchScript.At(Duration(settings)));
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void A_contact_shorter_than_the_minimum_contact_is_ignored_as_too_short(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Tall);
        var shortest =
            settings.MinContact > TimeSpan.Zero ? settings.MinContact.TotalMilliseconds : 1;

        script.Tap(1, 180, 180, atMs: 0, durationMs: shortest - 1);
        script.Tap(2, 180, 180, atMs: 5_000, durationMs: shortest);

        if (settings.MinContact > TimeSpan.Zero)
        {
            script.Events[0].Kind.ShouldBe(GestureKind.Ignored);
            script.Events[0].Ignored.ShouldBe(IgnoreReason.TooShort);
            script.Events[0].Target.ShouldBe(new TouchTargetId(1));
        }
        else
        {
            script
                .Events[0]
                .Kind.ShouldBe(GestureKind.Tap, "a zero minimum contact is «Desactivado»");
        }

        script.Events[1].Kind.ShouldBe(GestureKind.Tap);
    }

    [Theory]
    [MemberData(nameof(PresetsAndScales))]
    public void Moving_up_to_the_cancel_distance_taps_and_moving_further_is_ignored(
        string preset,
        double dpi
    )
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, dpi, Tall);
        var duration = Duration(settings);
        var cancel = (int)Math.Floor(settings.CancelMovePx * dpi);

        // Vertical moves never make a swipe, so only the cancel distance decides.
        script.Down(1, 180, 150, 0).Move(1, 180, 150 + cancel, duration / 2).Up(1, duration);
        script
            .Down(2, 180, 150, 5_000)
            .Move(2, 180, 150 + cancel + 1, 5_000 + (duration / 2))
            .Up(2, 5_000 + duration);

        script.Events[0].Kind.ShouldBe(GestureKind.Tap);
        if (settings.CancelMovePx > 0)
        {
            script.Events[1].Kind.ShouldBe(GestureKind.Ignored);
            script.Events[1].Ignored.ShouldBe(IgnoreReason.Moved);
        }
        else
        {
            script
                .Events[1]
                .Kind.ShouldBe(GestureKind.Tap, "a zero cancel distance is «Desactivado»");
        }
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void A_second_tap_on_the_same_target_within_the_debounce_is_ignored(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Tall);
        var duration = Duration(settings);
        var debounce = settings.Debounce.TotalMilliseconds;

        script.Tap(1, 180, 180, atMs: 0, duration); // accepted when it lifts, at «duration»
        script.Tap(2, 180, 180, atMs: debounce - 1, duration); // lifts 1 ms before the window ends

        script.Kinds.ShouldBe([GestureKind.Tap, GestureKind.Ignored]);
        script.Events[1].Ignored.ShouldBe(IgnoreReason.Debounced);
        script.Events[1].Target.ShouldBe(new TouchTargetId(1));
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void Bounces_are_ignored_and_never_restart_the_debounce_window(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Tall);
        var duration = Duration(settings);
        var debounce = settings.Debounce.TotalMilliseconds;

        script.Tap(1, 180, 180, atMs: 0, duration); // accepted at «duration»
        script.Tap(2, 180, 180, atMs: duration + 1, duration); // a bounce right after it
        script.Tap(3, 180, 180, atMs: debounce - 1, duration); // a bounce that lifts 1 ms before the window ends

        // Lifts at debounce + duration - 1 + duration: out of the window of the FIRST tap, but inside the window that
        // either bounce would have opened if it had restarted it.
        script.Tap(4, 180, 180, atMs: debounce - 1 + duration, duration);

        script.Kinds.ShouldBe([
            GestureKind.Tap,
            GestureKind.Ignored,
            GestureKind.Ignored,
            GestureKind.Tap,
        ]);
        script.Events[1].Ignored.ShouldBe(IgnoreReason.Debounced);
        script.Events[2].Ignored.ShouldBe(IgnoreReason.Debounced);
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void The_debounce_of_one_target_never_blocks_the_others(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Tall, Other);
        var duration = Duration(settings);

        // Each tap lifts right before the next one goes down, all within the debounce of the first.
        script.Tap(1, 180, 180, atMs: 0, duration);
        script.Tap(2, 480, 180, atMs: duration + 1, duration);
        script.Tap(3, 180, 180, atMs: (2 * duration) + 2, duration);

        script.Kinds.ShouldBe([GestureKind.Tap, GestureKind.Tap, GestureKind.Ignored]);
        script.Events[1].Target.ShouldBe(new TouchTargetId(2));
        script.Events[2].Target.ShouldBe(new TouchTargetId(1));
        script.Events[2].Ignored.ShouldBe(IgnoreReason.Debounced);
    }

    [Theory]
    [MemberData(nameof(PresetsAndScales))]
    [Trait("Req", "REG-02")]
    public void A_touch_in_a_gap_within_the_extra_area_activates_the_nearest_target(
        string preset,
        double dpi
    )
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, dpi, Tall, Other);
        var slop = (int)Math.Round(settings.HitSlopPx * dpi, MidpointRounding.AwayFromZero);
        var duration = Duration(settings);

        // The first tile ends at x = 259; the second starts at 400.
        script.Tap(1, 259 + slop, 180, atMs: 0, duration);
        script.Tap(2, 259 + slop + 1, 180, atMs: 5_000, duration);
        script.Tap(3, 400 - slop, 180, atMs: 10_000, duration);

        script.Events[0].Kind.ShouldBe(GestureKind.Tap);
        script.Events[0].Target.ShouldBe(new TouchTargetId(1));
        script.Events[1].Kind.ShouldBe(GestureKind.Ignored);
        script.Events[1].Ignored.ShouldBe(IgnoreReason.NoTarget);
        script.Events[1].Target.ShouldBeNull();
        script.Events[2].Kind.ShouldBe(GestureKind.Tap);
        script.Events[2].Target.ShouldBe(new TouchTargetId(2));
    }

    [Fact]
    public void A_tap_that_lifts_outside_the_extra_area_is_ignored_even_with_the_cancel_distance_off()
    {
        var settings = TouchPresetSettings.PersonalSettings; // cancel 0, extra area 20
        var script = new TouchScript(settings, Tall);

        script.Down(1, 180, 250, 0).Move(1, 180, 285, 60).Up(1, 180, 250, 200);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.Moved);
        ignored.Target.ShouldBe(new TouchTargetId(1));
    }

    [Fact]
    public void A_tap_target_never_long_presses()
    {
        var script = new TouchScript(
            TouchPresetSettings.Get("standard"),
            TouchScript.Tile(1, 100, 100, kind: TouchTargetKind.Tap)
        );

        script.Down(1, 140, 140, 0).Tick(5_000).Up(1, 5_000);

        script.Single().Kind.ShouldBe(GestureKind.Tap);
    }

    /// <summary>A tap duration that passes the minimum contact of <paramref name="settings"/>.</summary>
    private static double Duration(TouchSettings settings) =>
        Math.Max(40, settings.MinContact.TotalMilliseconds);
}
