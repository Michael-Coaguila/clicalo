using System.Globalization;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// The whole pointer layer on a real surface with a synthetic finger (ADR-0006, blueprint §7.1, §7.8): tap, long
/// press, hold and swipe become the gestures of the recognizer, and the time from Windows recording the input to the
/// surface receiving the gesture stays within the budget of NFR-001 (p95 ≤ 50 ms, S2 measures it on real hardware).
/// </summary>
[Trait("Requires", "Desktop")]
[Collection(DesktopCollectionDefinition.Name)]
public sealed class GestureDesktopTests(PointerDesktopFixture fixture)
    : IClassFixture<PointerDesktopFixture>
{
    /// <summary>Taps measured for the latency percentiles.</summary>
    private const int LatencyTaps = 20;

    /// <summary>The p95 budget from the finger lifting to the gesture reaching the surface (§7.1).</summary>
    private static readonly TimeSpan LatencyBudget = TimeSpan.FromMilliseconds(50);

    /// <summary>Largest delay of a timer-driven gesture (long press, hold start) past its deadline.</summary>
    private static readonly TimeSpan TimerSlack = TimeSpan.FromMilliseconds(250);

    [DesktopFact]
    [Trait("Req", "TAC-002")]
    public async Task A_tap_on_a_tile_becomes_a_tap_on_that_tile()
    {
        await fixture.PrepareAsync();
        var at = fixture.CenterOf(3);

        using (var finger = PointerDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            finger.Tap(at.X, at.Y);
        }

        var tap = (await GesturesAsync(1))[0].Gesture;
        tap.Kind.ShouldBe(GestureKind.Tap);
        tap.Target.ShouldBe(new TouchTargetId(3));
    }

    [DesktopFact]
    [Trait("Req", "CUA-014")]
    public async Task Resting_on_a_tile_opens_its_long_press_and_no_tap_follows()
    {
        await fixture.PrepareAsync();
        var at = fixture.CenterOf(1);

        using (var finger = PointerDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            finger.Hold(at.X, at.Y, Timings.Touch.LongPress + TimeSpan.FromMilliseconds(300));
        }

        await WaitForLiftAsync();
        var longPress = fixture.Recorder.Gestures.ShouldHaveSingleItem();
        longPress.Gesture.Kind.ShouldBe(GestureKind.LongPress);
        longPress.Gesture.Target.ShouldBe(new TouchTargetId(1));
        var down = fixture.Recorder.Samples.First(s => s.Phase == PointerPhase.Down).Timestamp;
        (longPress.Gesture.Timestamp - down).ShouldBeInRange(
            Timings.Touch.LongPress,
            Timings.Touch.LongPress + TimeSpan.FromMilliseconds(50)
        );
        longPress.Latency.ShouldBeLessThan(TimerSlack, "the deadline timer runs late");
        longPress.ThreadId.ShouldBe(WpfThread.Invoke(() => Environment.CurrentManagedThreadId));
    }

    [DesktopFact]
    [Trait("Req", "EJE-004")]
    public async Task A_hold_starts_with_the_contact_and_ends_when_the_finger_lifts()
    {
        await fixture.PrepareAsync();
        var at = fixture.CenterOf(2);
        var held = TimeSpan.FromMilliseconds(700);

        using (var finger = PointerDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            finger.Hold(at.X, at.Y, held);
        }

        var gestures = await GesturesAsync(2);
        gestures.Select(g => g.Gesture.Kind).ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        gestures.ShouldAllBe(g => g.Gesture.Target == new TouchTargetId(2));
        gestures[1].Gesture.HoldEnd.ShouldBe(HoldEndReason.Lifted);
        (
            gestures[1].Gesture.Timestamp - gestures[0].Gesture.Timestamp
        ).ShouldBeGreaterThanOrEqualTo(held - TimeSpan.FromMilliseconds(50));
    }

    [DesktopFact]
    [Trait("Req", "CUA-005")]
    public async Task A_horizontal_drag_is_a_swipe_and_not_a_tap()
    {
        await fixture.PrepareAsync();
        var from = fixture.CenterOf(4);
        var distance = (int)
            Math.Ceiling(
                (Timings.Touch.SwipeMinDistancePx + 40)
                    * WpfThread.Invoke(() => fixture.Surface.DpiScale)
            );

        using (var finger = PointerDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            finger.Drag(from.X, from.Y, from.X - distance, from.Y, TimeSpan.FromMilliseconds(200));
        }

        await WaitForLiftAsync();
        var swipe = fixture.Recorder.Gestures.ShouldHaveSingleItem().Gesture;
        swipe.Kind.ShouldBe(GestureKind.Swipe);
        swipe.Swipe.ShouldBe(SwipeDirection.Left);
    }

    [DesktopFact]
    [Trait("Req", "NFR-001")]
    public async Task Tap_to_gesture_latency_stays_within_the_budget()
    {
        await fixture.PrepareAsync();
        var at = fixture.CenterOf(3);
        var spacing = PointerDesktopFixture.Settings.Debounce + TimeSpan.FromMilliseconds(50);

        using (var finger = PointerDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            for (var i = 1; i <= LatencyTaps; i++)
            {
                finger.Tap(at.X, at.Y);
                await GesturesAsync(i);
                await Task.Delay(spacing, TestContext.Current.CancellationToken);
            }
        }

        var gestures = fixture.Recorder.Gestures;
        gestures.ShouldAllBe(g => g.Gesture.Kind == GestureKind.Tap);
        var ups = fixture
            .Recorder.Frames.Where(f => f.Frame.Samples.Any(s => s.Phase == PointerPhase.Up))
            .ToList();
        var report = new LatencyReport(
            "pointer-tap-latency",
            [.. gestures.Select(g => g.Latency.TotalMilliseconds)]
        );
        var queueing = new LatencyReport(
            "pointer-frame-queueing",
            [.. ups.Select(f => (f.ReceivedAt - f.Frame.Timestamp).TotalMilliseconds)]
        );
        var context = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["device"] = "finger",
            ["dpiScale"] = WpfThread
                .Invoke(() => fixture.Surface.DpiScale)
                .ToString(CultureInfo.InvariantCulture),
            ["frameQueueingP95Ms"] = queueing.P95.ToString("0.0", CultureInfo.InvariantCulture),
            ["stampedByWindows"] = (queueing.Max > 0).ToString(CultureInfo.InvariantCulture),
            ["budgetMs"] = LatencyBudget.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
        };
        report.Record(context);

        report.P95.ShouldBeLessThanOrEqualTo(
            LatencyBudget.TotalMilliseconds,
            report.Summary(context)
        );
    }

    /// <summary>Waits for at least <paramref name="count"/> gestures and returns them.</summary>
    private async Task<IReadOnlyList<RecordedGesture>> GesturesAsync(int count)
    {
        await PointerDesktopFixture.WaitUntilAsync(
            () => fixture.Recorder.Gestures.Count >= count,
            string.Create(CultureInfo.InvariantCulture, $"expected {count} gestures")
        );
        return fixture.Recorder.Gestures;
    }

    private Task WaitForLiftAsync() =>
        PointerDesktopFixture.WaitUntilAsync(
            () =>
                fixture.Recorder.Samples.Any(s => s.Phase is PointerPhase.Up or PointerPhase.Cancel)
                && WpfThread.Invoke(() => fixture.Host.Recognizer.ActiveContacts) == 0,
            "the contact never lifted"
        );
}
