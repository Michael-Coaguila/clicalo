using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// A Hold target inside a zone that scrolls (TAC-004, EJE-004: «Pinned» of the Tab view): it does not start holding
/// until the gesture is clearly not a scroll. The contact must rest <c>Timings.Touch.HoldInScrollDelay</c> without
/// moving past the drag threshold; a finger that scrolls never presses a key (REG-03 by construction: what was never
/// pressed needs no release).
/// </summary>
[Trait("Req", "TAC-004")]
[Trait("Req", "EJE-004")]
public sealed class GestureRecognizerScrollZoneHoldTests
{
    private static readonly double Delay = Timings.Touch.HoldInScrollDelay.TotalMilliseconds;

    // [100, 180) × [100, 400): tall, so a finger can slide along it without leaving it.
    private static readonly TouchTarget InZone = TouchScript.Tile(
        1,
        100,
        100,
        height: 300,
        kind: TouchTargetKind.Hold
    ) with
    {
        InScrollZone = true,
    };

    private static readonly TouchTarget Outside = InZone with { InScrollZone = false };

    [Fact]
    public void In_a_zone_that_scrolls_a_hold_waits_until_the_finger_has_rested()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), InZone); // no minimum contact

        script.Down(1, 140, 140, 0);
        script.Events.ShouldBeEmpty();
        script.Recognizer.NextDeadline.ShouldBe(TouchScript.At(Delay));
        script.Tick(Delay - 1);
        script.Events.ShouldBeEmpty();
        script.Tick(Delay);
        script.Up(1, 2_000);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        script.Events[0].Timestamp.ShouldBe(TouchScript.At(Delay));
        script.Events[1].HoldEnd.ShouldBe(HoldEndReason.Lifted);
    }

    [Fact]
    public void Outside_a_zone_that_scrolls_the_same_finger_holds_at_once()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), Outside);

        script.Down(1, 140, 140, 0);

        script.Kinds.ShouldBe([GestureKind.HoldStart]);
        script.Events[0].Timestamp.ShouldBe(TouchScript.At(0));
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    [Trait("Req", "REG-03")]
    public void A_finger_that_scrolls_never_starts_the_hold(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, InZone);

        // Past the drag threshold, max(6, cancel distance), along the zone and still on the target.
        var slide = Math.Max(Timings.Touch.DragMinDistancePx, settings.CancelMovePx) + 1;
        script
            .Down(1, 140, 140, 0)
            .Move(1, 140, 140 + (int)slide, 60)
            .Tick(1_000)
            .Move(1, 140, 300, 1_100)
            .Up(1, 1_200);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.Moved);
    }

    [Fact]
    public void A_finger_that_trembles_within_the_drag_threshold_still_holds()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), InZone); // threshold 45

        script.Down(1, 140, 140, 0).Move(1, 148, 160, 70).Tick(Delay).Up(1, 900);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
    }

    [Fact]
    public void Lifting_before_the_wait_ends_presses_nothing()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), InZone);

        script.Down(1, 140, 140, 0).Up(1, Delay - 1).Tick(1_000);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.TooShort);
    }

    [Fact]
    [Trait("Req", "TAC-002")]
    public void The_longer_of_the_minimum_contact_and_the_wait_applies()
    {
        // «Temblor fuerte» asks for 80 ms of contact: in the zone the hold still waits the whole delay.
        var strong = TouchPresetSettings.Get("strong-tremor");
        strong.MinContact.ShouldBeLessThan(Timings.Touch.HoldInScrollDelay);
        var script = new TouchScript(strong, InZone);

        script.Down(1, 140, 140, 0).Tick(strong.MinContact.TotalMilliseconds);
        script.Events.ShouldBeEmpty();
        script.Tick(Delay);

        script.Kinds.ShouldBe([GestureKind.HoldStart]);

        // A minimum contact longer than the delay is kept as it is.
        var slow = strong with
        {
            MinContact = TimeSpan.FromMilliseconds(Delay + 100),
        };
        var patient = new TouchScript(slow, InZone);
        patient.Down(1, 140, 140, 0).Tick(Delay);
        patient.Events.ShouldBeEmpty();
        patient.Tick(Delay + 100);
        patient.Kinds.ShouldBe([GestureKind.HoldStart]);
    }
}
