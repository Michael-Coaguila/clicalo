using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// Hold targets (EJE-004): the start waits for the minimum contact and respects the debounce (TAC-002, DIS-20), and
/// every start ends exactly once, when the finger lifts, is cancelled, leaves the extra hit area or the recognizer
/// is reset, so no key stays down (REG-03). Several holds are independent (EJE-006).
/// </summary>
[Trait("Req", "EJE-004")]
[Trait("Req", "TAC-002")]
public sealed class GestureRecognizerHoldTests
{
    // [100, 180) × [100, 180) and [300, 380) × [100, 180).
    private static readonly TouchTarget Shift = TouchScript.Tile(
        1,
        100,
        100,
        kind: TouchTargetKind.Hold
    );

    private static readonly TouchTarget Ctrl = TouchScript.Tile(
        2,
        300,
        100,
        kind: TouchTargetKind.Hold
    );

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void A_hold_starts_after_the_minimum_contact_and_ends_when_the_finger_lifts(
        string preset
    )
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Shift);
        var minContact = settings.MinContact.TotalMilliseconds;

        script.Down(1, 140, 140, 0);
        if (minContact > 0)
        {
            script.Events.ShouldBeEmpty();
            script.Recognizer.NextDeadline.ShouldBe(TouchScript.At(minContact));
            script.Tick(minContact - 1);
            script.Events.ShouldBeEmpty();
            script.Tick(minContact);
        }

        script.Up(1, 3_000);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        script.Events[0].Timestamp.ShouldBe(TouchScript.At(minContact));
        script.Events[0].Target.ShouldBe(new TouchTargetId(1));
        script.Events[1].HoldEnd.ShouldBe(HoldEndReason.Lifted);
        script.Events[1].Timestamp.ShouldBe(TouchScript.At(3_000));
    }

    [Fact]
    public void Lifting_before_the_minimum_contact_never_presses()
    {
        var script = new TouchScript(TouchPresetSettings.Get("strong-tremor"), Shift); // 80 ms

        script.Down(1, 140, 140, 0).Up(1, 79).Tick(1_000);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.TooShort);
    }

    [Fact]
    public void A_late_timer_still_starts_the_hold_at_the_minimum_contact()
    {
        var script = new TouchScript(TouchPresetSettings.Get("strong-tremor"), Shift);

        script.Down(1, 140, 140, 0).Up(1, 500);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        script.Events[0].Timestamp.ShouldBe(TouchScript.At(80));
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void The_debounce_applies_to_the_start_of_the_hold(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var script = new TouchScript(settings, Shift);
        var minContact = settings.MinContact.TotalMilliseconds;
        var debounce = settings.Debounce.TotalMilliseconds;

        // First start at «minContact»; a bounce whose start would fall 1 ms before the window ends.
        script.Down(1, 140, 140, 0).Up(1, minContact + 50);
        script.Down(2, 140, 140, debounce - 1).Up(2, debounce + minContact + 100);

        script.Kinds.ShouldBe(
            [GestureKind.HoldStart, GestureKind.HoldEnd, GestureKind.Ignored],
            "the bounce never presses the key"
        );
        script.Events[2].Ignored.ShouldBe(IgnoreReason.Debounced);
    }

    [Fact]
    public void Moving_inside_the_extra_area_keeps_holding_even_past_the_cancel_distance()
    {
        var script = new TouchScript(TouchPresetSettings.Get("strong-tremor"), Shift); // extra area 24, cancel 28

        script.Down(1, 110, 110, 0).Tick(80).Move(1, 199, 199, 200).Move(1, 76, 76, 300).Up(1, 400);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        script.Events[1].HoldEnd.ShouldBe(HoldEndReason.Lifted);
    }

    [Fact]
    public void Leaving_the_extra_area_releases_the_hold_at_once()
    {
        var script = new TouchScript(TouchPresetSettings.Get("mild-tremor"), Shift); // extra area 14

        script.Down(1, 140, 140, 0).Move(1, 193, 140, 100); // 180 + 14 = 194 is the first column outside
        script.Kinds.ShouldBe([GestureKind.HoldStart]);

        script.Move(1, 194, 140, 150);
        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        script.Events[1].HoldEnd.ShouldBe(HoldEndReason.LeftTarget);
        script.Events[1].Timestamp.ShouldBe(TouchScript.At(150));
        script.Events[1].Position.ShouldBe(new PhysicalPoint(194, 140));

        // Coming back does not press again, and lifting reports nothing more.
        script.Move(1, 140, 140, 200).Up(1, 300);
        script.Events.Count.ShouldBe(2);
    }

    [Fact]
    public void A_cancelled_contact_releases_the_hold()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), Shift);

        script.Down(1, 140, 140, 0).Cancel(1, 100).Up(1, 200);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        script.Events[1].HoldEnd.ShouldBe(HoldEndReason.Canceled);
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public void A_reset_releases_every_active_hold_and_forgets_the_contacts()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), Shift, Ctrl);
        script.Down(1, 140, 140, 0).Down(2, 340, 140, 10).Down(3, 500, 500, 20);

        script.Reset(100);

        script.Kinds.ShouldBe([
            GestureKind.HoldStart,
            GestureKind.HoldStart,
            GestureKind.HoldEnd,
            GestureKind.HoldEnd,
        ]);
        script.Events.Skip(2).ShouldAllBe(e => e.HoldEnd == HoldEndReason.Reset);
        script.Recognizer.ActiveContacts.ShouldBe(0);
        script.Recognizer.NextDeadline.ShouldBeNull();

        script.ClearEvents().Up(1, 200).Up(2, 210).Up(3, 220);
        script.Events.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EJE-006")]
    public void Two_holds_are_independent_and_each_ends_with_its_own_contact()
    {
        var script = new TouchScript(TouchPresetSettings.Get("mild-tremor"), Shift, Ctrl);

        script.Down(1, 140, 140, 0).Down(2, 340, 140, 30);
        script.Recognizer.ActiveContacts.ShouldBe(2);
        script.Up(1, 500);

        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.HoldStart, GestureKind.HoldEnd]);
        script.Events[2].PointerId.ShouldBe(1u);
        script.Events[2].Target.ShouldBe(new TouchTargetId(1));
        script.Recognizer.ActiveContacts.ShouldBe(1);

        script.Up(2, 900);
        script.Events[3].Kind.ShouldBe(GestureKind.HoldEnd);
        script.Events[3].PointerId.ShouldBe(2u);
    }

    [Fact]
    [Trait("Req", "ACC-007")]
    public void A_palm_on_a_hold_never_presses()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), Shift);

        script.Down(1, 140, 140, 0, contactSize: 200).Up(1, 500);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.Palm);
    }
}
