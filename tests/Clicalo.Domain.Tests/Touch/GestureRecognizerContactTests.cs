using System.Collections.Immutable;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// Contact bookkeeping of the recognizer: palms (ACC-007), cancellations, the layout frozen under the finger
/// (PAN-009), settings changes, deadlines and argument checks.
/// </summary>
public sealed class GestureRecognizerContactTests
{
    private static readonly TouchTarget First = TouchScript.Tile(1, 100, 100);
    private static readonly TouchTarget Second = TouchScript.Tile(2, 300, 100);

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [Trait("Req", "ACC-007")]
    public void A_palm_sized_contact_is_ignored_as_a_palm(double dpi)
    {
        var palm = (int)Math.Ceiling(Timings.Touch.PalmContactMinPx * dpi);
        var script = new TouchScript(TouchPresetSettings.Get("standard"), dpi, First);

        script.Down(1, 140, 140, 0, contactSize: palm - 1).Up(1, 50);
        script.Down(2, 140, 140, 1_000, contactSize: palm).Up(2, 1_050);

        script.Kinds.ShouldBe([GestureKind.Tap, GestureKind.Ignored]);
        script.Events[1].Ignored.ShouldBe(IgnoreReason.Palm);
        script.Events[1].Target.ShouldBe(new TouchTargetId(1));
    }

    [Fact]
    [Trait("Req", "ACC-007")]
    public void A_contact_that_grows_into_a_palm_is_ignored_and_never_swipes()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), First);

        script.Down(1, 140, 140, 0).Move(1, 140, 140, 30, contactSize: 300).Move(1, 60, 140, 60);
        script.Up(1, 40, 140, 90);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.Palm);
    }

    [Fact]
    public void A_cancelled_contact_is_ignored_and_reports_nothing_afterwards()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), First);

        script.Down(1, 140, 140, 0).Cancel(1, 30).Tick(5_000).Up(1, 5_000);

        var ignored = script.Single();
        ignored.Kind.ShouldBe(GestureKind.Ignored);
        ignored.Ignored.ShouldBe(IgnoreReason.Canceled);
        script.Recognizer.ActiveContacts.ShouldBe(0);
    }

    [Fact]
    public void A_second_down_with_the_same_identifier_cancels_the_lost_contact()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), First, Second);

        script.Down(1, 140, 140, 0).Down(1, 340, 140, 100).Up(1, 150);

        script.Kinds.ShouldBe([GestureKind.Ignored, GestureKind.Tap]);
        script.Events[0].Ignored.ShouldBe(IgnoreReason.Canceled);
        script.Events[1].Target.ShouldBe(new TouchTargetId(2));
    }

    [Fact]
    [Trait("Req", "PAN-009")]
    public void A_contact_keeps_the_target_it_went_down_on_when_the_layout_changes()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), First);

        script.Down(1, 140, 140, 0);
        script.Recognizer.ActiveContacts.ShouldBe(1);

        // The layout moves another tile under the finger; the contact still belongs to tile 1.
        script.SetTargets(TouchScript.Tile(5, 100, 100), TouchScript.Tile(1, 500, 100));
        script.Up(1, 50);
        script.Down(2, 140, 140, 1_000).Up(2, 1_050);

        script.Kinds.ShouldBe([GestureKind.Tap, GestureKind.Tap]);
        script.Events[0].Target.ShouldBe(new TouchTargetId(1));
        script.Events[1].Target.ShouldBe(new TouchTargetId(5));
    }

    [Fact]
    [Trait("Req", "TAC-002")]
    public void The_filter_memory_of_a_target_survives_a_layout_change()
    {
        var script = new TouchScript(TouchPresetSettings.Get("mild-tremor"), First, Second);

        script.Tap(1, 140, 140, atMs: 0);
        script.SetTargets(TouchScript.Tile(1, 100, 300), TouchScript.Tile(7, 300, 300));
        script.Tap(2, 140, 340, atMs: 100);

        script.Kinds.ShouldBe([GestureKind.Tap, GestureKind.Ignored]);
        script.Events[1].Ignored.ShouldBe(IgnoreReason.Debounced);
    }

    [Fact]
    public void A_contact_finishes_with_the_values_it_started_with()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), First); // cancel 45

        script.Down(1, 140, 110, 0);
        script.Recognizer.Configure(TouchPresetSettings.Get("strong-tremor"), 1.0); // cancel 28, minimum 80 ms
        script.Move(1, 140, 150, 20).Up(1, 40);
        script.Down(2, 140, 110, 1_000).Move(2, 140, 150, 1_020).Up(2, 1_100);

        script.Kinds.ShouldBe([GestureKind.Tap, GestureKind.Ignored]);
        script.Events[1].Ignored.ShouldBe(IgnoreReason.Moved);
        script.Recognizer.Settings.ShouldBe(TouchPresetSettings.Get("strong-tremor"));
    }

    [Fact]
    public void The_next_deadline_is_the_earliest_pending_one()
    {
        var hold = TouchScript.Tile(3, 500, 100, kind: TouchTargetKind.Hold);
        var script = new TouchScript(TouchPresetSettings.Get("strong-tremor"), First, hold);

        script.Recognizer.NextDeadline.ShouldBeNull();
        script.Down(1, 140, 140, 0);
        script.Recognizer.NextDeadline.ShouldBe(TouchScript.At(600));
        script.Down(2, 540, 140, 100);
        script.Recognizer.NextDeadline.ShouldBe(TouchScript.At(180));
        script.Tick(180);
        script.Recognizer.NextDeadline.ShouldBe(TouchScript.At(600));
        script.Tick(600);
        script.Recognizer.NextDeadline.ShouldBeNull();
        script.Kinds.ShouldBe([GestureKind.HoldStart, GestureKind.LongPress]);
    }

    [Fact]
    public void Frames_that_arrive_out_of_order_never_move_time_backwards()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), First);

        script.Down(1, 140, 140, 100).Up(1, 50);

        var tap = script.Single();
        tap.Kind.ShouldBe(GestureKind.Tap);
        tap.Timestamp.ShouldBe(TouchScript.At(100));
    }

    [Fact]
    public void A_frame_without_samples_only_advances_time()
    {
        var script = new TouchScript(TouchPresetSettings.Get("standard"), First);
        script.Down(1, 140, 140, 0);

        script.Recognizer.Feed(new PointerFrame(9, TouchScript.At(700), default), []);
        script.Recognizer.Feed(new PointerFrame(9, TouchScript.At(700), []), []);

        script.Recognizer.NextDeadline.ShouldBeNull("the long press was reported at 600 ms");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void The_dpi_scale_must_be_a_positive_finite_number(double dpi)
    {
        var settings = TouchPresetSettings.Get("standard");

        Should.Throw<ArgumentOutOfRangeException>(() => new GestureRecognizer(settings, dpi));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new GestureRecognizer(settings, 1.0).Configure(settings, dpi)
        );
    }

    [Fact]
    public void Negative_filter_values_are_rejected()
    {
        var negative = new TouchSettings(TimeSpan.FromMilliseconds(-1), 0, 0, TimeSpan.Zero);

        Should.Throw<ArgumentOutOfRangeException>(() => new GestureRecognizer(negative, 1.0));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new GestureRecognizer(negative with { Debounce = TimeSpan.Zero, HitSlopPx = -1 }, 1.0)
        );
    }

    [Fact]
    public void Two_targets_cannot_share_an_identifier()
    {
        var recognizer = new GestureRecognizer(TouchPresetSettings.Get("standard"), 1.0);

        Should.Throw<ArgumentException>(() =>
            recognizer.SetTargets([TouchScript.Tile(1, 0, 0), TouchScript.Tile(1, 100, 0)])
        );
        recognizer.SetTargets(default(ImmutableArray<TouchTarget>));
        recognizer.Targets.ShouldBeEmpty();
    }

    [Fact]
    public void Output_lists_are_required()
    {
        var recognizer = new GestureRecognizer(TouchPresetSettings.Get("standard"), 1.0);
        var frame = new PointerFrame(1, TouchScript.At(0), []);

        Should.Throw<ArgumentNullException>(() => recognizer.Feed(frame, null!));
        Should.Throw<ArgumentNullException>(() => recognizer.OnTick(TouchScript.At(0), null!));
        Should.Throw<ArgumentNullException>(() => recognizer.Reset(TouchScript.At(0), null!));
    }
}
