using System.Diagnostics;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// A synthetic finger never goes down right after a synthetic pen left: Windows drops that touch (pen and touch
/// arbitration), which made the first finger tap of <c>NonActivationTests</c> after the pen case fail. Headless.
/// </summary>
public sealed class PenTouchSettleTests
{
    [Fact]
    public void Without_a_pen_a_finger_never_waits()
    {
        new PenTouchSettle().RemainingBeforeTouch(Stopwatch.GetTimestamp()).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Right_after_a_pen_left_a_finger_waits_the_whole_window()
    {
        var settle = new PenTouchSettle();
        var left = Stopwatch.GetTimestamp();
        settle.PenLeft(left);

        settle.RemainingBeforeTouch(left).ShouldBe(PenTouchSettle.Window);
    }

    [Fact]
    public void A_finger_waits_only_what_is_left_of_the_window()
    {
        var settle = new PenTouchSettle();
        var left = Stopwatch.GetTimestamp();
        settle.PenLeft(left);
        var later = left + (Stopwatch.Frequency / 4);

        settle
            .RemainingBeforeTouch(later)
            .ShouldBe(PenTouchSettle.Window - Stopwatch.GetElapsedTime(left, later));
        settle.RemainingBeforeTouch(left + (Stopwatch.Frequency * 2)).ShouldBe(TimeSpan.Zero);
    }
}
