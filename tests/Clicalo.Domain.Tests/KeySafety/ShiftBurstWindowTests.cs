using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Timing;
using CsCheck;

namespace Clicalo.Domain.Tests.KeySafety;

/// <summary>SEG-008: never five Shift presses inside one second, so Windows Sticky Keys never triggers.</summary>
[Trait("Req", "SEG-008")]
public sealed class ShiftBurstWindowTests
{
    private const long Second = 1_000;

    [Fact]
    public void The_limit_is_four_presses_per_second() =>
        Timings.KeySafety.ShiftBurstLimit.ShouldBe(new CountWindow(4, TimeSpan.FromSeconds(1)));

    [Fact]
    public void The_fifth_press_waits_until_the_first_leaves_the_window()
    {
        var window = ShiftBurstWindow.Empty;
        foreach (var at in new long[] { 0, 100, 200, 300 })
        {
            window.NextAllowed(at, 4, Second).ShouldBe(at);
            window = window.Record(at, Second);
        }

        window.NextAllowed(400, 4, Second).ShouldBe(1_000);
        window.NextAllowed(1_000, 4, Second).ShouldBe(1_000);
    }

    [Fact]
    public void Presses_that_left_the_window_are_forgotten() =>
        ShiftBurstWindow
            .Empty.Record(0, Second)
            .Record(500, Second)
            .Record(1_600, Second)
            .RecentShiftTicks.Items.ShouldBe([1_600L]);

    [Fact]
    public void Pressing_whenever_allowed_never_puts_five_presses_in_one_window() =>
        Gen.Long[0, 3 * Second]
            .Array[1, 40]
            .Sample(requests =>
            {
                var window = ShiftBurstWindow.Empty;
                var sent = new List<long>();
                var now = 0L;
                foreach (var request in requests.Order())
                {
                    now = Math.Max(now, request);
                    now = window.NextAllowed(now, 4, Second);
                    window = window.Record(now, Second);
                    sent.Add(now);
                }

                foreach (var at in sent)
                {
                    sent.Count(t => t > at - Second && t <= at).ShouldBeLessThanOrEqualTo(4);
                }
            });
}
