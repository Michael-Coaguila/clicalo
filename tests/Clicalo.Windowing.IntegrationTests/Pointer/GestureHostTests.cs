using System.Collections.Immutable;
using System.Windows.Threading;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Time;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Pointer;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// The gesture pipeline of a surface on its dispatcher, with a simulated clock (NFR-020): frames become gestures in
/// order, deadlines run from the <see cref="TimeProvider"/> timer on the UI thread, a reset releases holds at once
/// and a disposed host stays silent.
/// </summary>
public sealed class GestureHostTests
{
    private static readonly TouchSettings Standard = new(
        TouchPresets.Find("standard")!.Debounce,
        TouchPresets.Find("standard")!.HitSlopPx,
        TouchPresets.Find("standard")!.CancelMovePx,
        TouchPresets.Find("standard")!.MinContact
    );

    private static readonly ImmutableArray<TouchTarget> Tiles =
    [
        new(
            new TouchTargetId(1),
            new PhysicalRect(100, 100, 80, 80),
            TouchTargetKind.TapOrLongPress
        ),
        new(new TouchTargetId(2), new PhysicalRect(200, 100, 80, 80), TouchTargetKind.Hold),
        new(new TouchTargetId(3), new PhysicalRect(300, 100, 80, 80), TouchTargetKind.Hold),
    ];

    [Fact]
    [Trait("Req", "TAC-002")]
    public void Frames_become_gestures_in_order_on_the_ui_thread()
    {
        using var run = new HostRun(Standard);

        run.Feed(1, PointerPhase.Down, 140, 140);
        run.Clock.Advance(TimeSpan.FromMilliseconds(40));
        run.Feed(1, PointerPhase.Up, 140, 140);

        var tap = run.Gestures.ShouldHaveSingleItem();
        tap.Gesture.Kind.ShouldBe(GestureKind.Tap);
        tap.Gesture.Target.ShouldBe(new TouchTargetId(1));
        tap.ThreadId.ShouldBe(run.UiThreadId);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void The_long_press_comes_from_the_deadline_timer_without_any_frame()
    {
        using var run = new HostRun(Standard);
        run.Feed(1, PointerPhase.Down, 140, 140);

        run.Clock.Advance(Timings.Touch.LongPress - TimeSpan.FromMilliseconds(1));
        HostRun.Drain();
        run.Gestures.ShouldBeEmpty();

        run.Clock.Advance(TimeSpan.FromMilliseconds(1));
        HostRun.Drain();

        var longPress = run.Gestures.ShouldHaveSingleItem();
        longPress.Gesture.Kind.ShouldBe(GestureKind.LongPress);
        longPress.ThreadId.ShouldBe(run.UiThreadId);
    }

    [Fact]
    [Trait("Req", "EJE-004")]
    [Trait("Req", "TAC-002")]
    public void A_hold_starts_from_the_timer_once_the_minimum_contact_has_passed()
    {
        var strong = TouchPresets.Find("strong-tremor")!;
        using var run = new HostRun(
            new TouchSettings(
                strong.Debounce,
                strong.HitSlopPx,
                strong.CancelMovePx,
                strong.MinContact
            )
        );
        run.Feed(1, PointerPhase.Down, 240, 140);
        HostRun.Drain();
        run.Gestures.ShouldBeEmpty();

        run.Clock.Advance(strong.MinContact);
        HostRun.Drain();

        run.Gestures.ShouldHaveSingleItem().Gesture.Kind.ShouldBe(GestureKind.HoldStart);
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public void A_reset_releases_every_hold_before_it_returns()
    {
        using var run = new HostRun(Standard);
        run.Feed(1, PointerPhase.Down, 240, 140);

        WpfThread.Invoke(run.Host.Reset);

        run.Gestures.Select(g => g.Gesture.Kind)
            .ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        run.Gestures[1].Gesture.HoldEnd.ShouldBe(HoldEndReason.Reset);
        WpfThread.Invoke(() => run.Host.Recognizer.ActiveContacts).ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public void A_handler_that_resets_the_surface_keeps_every_gesture_once_and_in_order()
    {
        using var run = new HostRun(Standard, resetOnHoldStart: true);

        run.Feed(1, PointerPhase.Down, 240, 140);

        run.Gestures.Select(g => g.Gesture.Kind)
            .ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public void A_handler_that_throws_on_one_hold_end_does_not_keep_the_next_one_from_arriving()
    {
        using var run = new HostRun(Standard, throwOnFirstHoldEnd: true);
        run.Feed(1, PointerPhase.Down, 240, 140);
        run.Feed(2, PointerPhase.Down, 340, 140);
        run.Clock.Advance(TimeSpan.FromMilliseconds(50));

        // Both fingers lift in the same frame and the surface fails on the first release.
        Should.Throw<InvalidOperationException>(() =>
            run.FeedFrame((1, PointerPhase.Up, 240, 140), (2, PointerPhase.Up, 340, 140))
        );

        run.Gestures.Where(g => g.Gesture.Kind == GestureKind.HoldEnd)
            .Select(g => g.Gesture.Target)
            .ShouldBe([new TouchTargetId(2), new TouchTargetId(3)]);
        WpfThread.Invoke(() => run.Host.Recognizer.ActiveContacts).ShouldBe(0);
    }

    [Fact]
    public void A_disposed_host_ignores_frames_and_timers()
    {
        var run = new HostRun(Standard);
        run.Feed(1, PointerPhase.Down, 140, 140);

        run.Dispose();
        run.Clock.Advance(TimeSpan.FromSeconds(5));
        HostRun.Drain();
        run.Feed(1, PointerPhase.Up, 140, 140);

        run.Gestures.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public void Disposing_the_host_on_the_ui_thread_releases_an_active_hold()
    {
        var run = new HostRun(Standard);
        run.Feed(1, PointerPhase.Down, 240, 140);

        run.Dispose();
        run.Feed(1, PointerPhase.Up, 240, 140);

        run.Gestures.Select(g => g.Gesture.Kind)
            .ShouldBe([GestureKind.HoldStart, GestureKind.HoldEnd]);
        run.Gestures[1].Gesture.HoldEnd.ShouldBe(HoldEndReason.Reset);
    }

    [Fact]
    public void The_host_needs_all_its_parts()
    {
        var recognizer = new GestureRecognizer(Standard, 1.0);
        var dispatcher = WpfThread.Dispatcher;
        Action<GestureEvent> onGesture = _ => { };

        Should.Throw<ArgumentNullException>(() =>
            new GestureHost(null!, dispatcher, TimeProvider.System, onGesture)
        );
        Should.Throw<ArgumentNullException>(() =>
            new GestureHost(recognizer, null!, TimeProvider.System, onGesture)
        );
        Should.Throw<ArgumentNullException>(() =>
            new GestureHost(recognizer, dispatcher, null!, onGesture)
        );
        Should.Throw<ArgumentNullException>(() =>
            new GestureHost(recognizer, dispatcher, TimeProvider.System, null!)
        );
    }

    /// <summary>A host on the WPF test thread with a fake clock, recording what it delivers.</summary>
    private sealed class HostRun : IDisposable
    {
        private readonly PointerRecorder _recorder;

        public HostRun(
            TouchSettings settings,
            bool resetOnHoldStart = false,
            bool throwOnFirstHoldEnd = false
        )
        {
            Clock = TestTime.CreateProvider();
            _recorder = new PointerRecorder(Clock);
            UiThreadId = WpfThread.Invoke(() => Environment.CurrentManagedThreadId);
            Host = WpfThread.Invoke(() =>
            {
                var recognizer = new GestureRecognizer(settings, 1.0);
                recognizer.SetTargets(Tiles);
                GestureHost? host = null;
                host = new GestureHost(
                    recognizer,
                    WpfThread.Dispatcher,
                    Clock,
                    gesture =>
                    {
                        _recorder.OnGesture(gesture);
                        if (resetOnHoldStart && gesture.Kind == GestureKind.HoldStart)
                        {
                            host!.Reset();
                        }

                        if (throwOnFirstHoldEnd && gesture.Kind == GestureKind.HoldEnd)
                        {
                            throwOnFirstHoldEnd = false;
                            throw new InvalidOperationException("The surface failed on a release.");
                        }
                    }
                );
                return host;
            });
        }

        public FakeTimeProvider Clock { get; }

        public GestureHost Host { get; }

        public int UiThreadId { get; }

        public IReadOnlyList<RecordedGesture> Gestures => _recorder.Gestures;

        /// <summary>Feeds a one-sample frame at the current fake time, on the UI thread.</summary>
        public void Feed(uint id, PointerPhase phase, int x, int y) => FeedFrame((id, phase, x, y));

        /// <summary>Feeds one frame with a sample per contact at the current fake time, on the UI thread.</summary>
        public void FeedFrame(params (uint Id, PointerPhase Phase, int X, int Y)[] contacts)
        {
            var now = Clock.GetUtcNow();
            ImmutableArray<PointerSample> samples =
            [
                .. contacts.Select(contact => new PointerSample(
                    contact.Id,
                    PointerKind.Finger,
                    contact.Phase,
                    new PhysicalPoint(contact.X, contact.Y),
                    new PhysicalRect(contact.X - 5, contact.Y - 5, 10, 10),
                    now,
                    PointerInputOrigin.Injected
                )),
            ];
            WpfThread.Invoke(() => Host.OnFrame(new PointerFrame(1, now, samples)));
        }

        /// <summary>Lets the dispatcher run the ticks the fake timer queued.</summary>
        public static void Drain() =>
            WpfThread.Dispatcher.Invoke(static () => { }, DispatcherPriority.Background);

        public void Dispose() => WpfThread.Invoke(Host.Dispose);
    }
}
