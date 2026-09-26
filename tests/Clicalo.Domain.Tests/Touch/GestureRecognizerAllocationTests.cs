using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// The recognizer runs inside the window procedure of every surface: after warming up, following contacts, taps,
/// long presses, holds and swipes allocates nothing (blueprint §7.8, NFR-001).
/// </summary>
[Trait("Req", "NFR-001")]
public sealed class GestureRecognizerAllocationTests
{
    [Fact]
    public void Feeding_frames_and_ticks_allocates_nothing_once_warm()
    {
        var recognizer = new GestureRecognizer(TouchPresetSettings.Get("mild-tremor"), 1.25);
        recognizer.SetTargets([
            TouchScript.Tile(1, 100, 100),
            TouchScript.Tile(2, 200, 100, kind: TouchTargetKind.Hold),
            TouchScript.Tile(3, 300, 100, kind: TouchTargetKind.Tap),
        ]);
        var output = new List<GestureEvent>(capacity: 1_024);
        var warmUp = Session(startMs: 0);
        var measured = Session(startMs: 60_000);

        Play(recognizer, warmUp, output);
        output.Clear();

        var before = GC.GetAllocatedBytesForCurrentThread();
        Play(recognizer, measured, output);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        output
            .Select(e => e.Kind)
            .ShouldBe([
                GestureKind.Tap,
                GestureKind.HoldStart,
                GestureKind.LongPress,
                GestureKind.HoldEnd,
                GestureKind.Swipe,
                GestureKind.Ignored,
                GestureKind.HoldStart,
                GestureKind.HoldEnd,
            ]);
    }

    private static void Play(
        GestureRecognizer recognizer,
        (PointerFrame Frame, double TickMs)[] session,
        List<GestureEvent> output
    )
    {
        foreach (var (frame, tickMs) in session)
        {
            recognizer.Feed(frame, output);
            if (tickMs > 0)
            {
                recognizer.OnTick(TouchScript.At(tickMs), output);
            }
        }

        _ = recognizer.NextDeadline;
        recognizer.Reset(TouchScript.At(session[^1].TickMs + 10_000), output);
    }

    /// <summary>
    /// Prebuilt frames (building them allocates) for: a tap on tile 1; a hold on tile 2 while a long press opens on
    /// tile 1 with two fingers; a swipe; a touch ignored by the swipe lock; a hold released by the reset.
    /// </summary>
    private static (PointerFrame Frame, double TickMs)[] Session(double startMs)
    {
        (PointerFrame, double) F(double ms, params PointerSample[] samples) =>
            (new PointerFrame(1, TouchScript.At(startMs + ms), [.. samples]), 0);

        (PointerFrame, double) FT(double ms, double tickMs, params PointerSample[] samples) =>
            (new PointerFrame(1, TouchScript.At(startMs + ms), [.. samples]), startMs + tickMs);

        PointerSample S(uint id, PointerPhase phase, int x, int y, double ms) =>
            TouchScript.Sample(id, phase, x, y, startMs + ms);

        return
        [
            F(0, S(1, PointerPhase.Down, 140, 140, 0)),
            F(20, S(1, PointerPhase.Move, 141, 141, 20)),
            F(40, S(1, PointerPhase.Up, 141, 141, 40)),
            F(
                1_000,
                S(2, PointerPhase.Down, 140, 140, 1_000),
                S(3, PointerPhase.Down, 240, 140, 1_000)
            ),
            FT(
                1_100,
                1_700,
                S(2, PointerPhase.Move, 142, 139, 1_100),
                S(3, PointerPhase.Move, 241, 140, 1_100)
            ),
            F(
                1_800,
                S(2, PointerPhase.Up, 142, 139, 1_800),
                S(3, PointerPhase.Up, 241, 140, 1_800)
            ),
            F(3_000, S(4, PointerPhase.Down, 380, 300, 3_000)),
            F(3_050, S(4, PointerPhase.Move, 330, 300, 3_050)),
            F(3_100, S(4, PointerPhase.Up, 280, 300, 3_100)),
            F(3_150, S(5, PointerPhase.Down, 340, 140, 3_150)),
            F(3_200, S(5, PointerPhase.Up, 340, 140, 3_200)),
            FT(5_000, 5_100, S(6, PointerPhase.Down, 240, 140, 5_000)),
        ];
    }
}
