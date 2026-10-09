using Clicalo.Domain.Tests.Generators;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>Test mode lasts 30 s and counts down by whole seconds; it marks the verdicts of the filter (TAC-008).</summary>
[Trait("Req", "TAC-008")]
public sealed class TestModeStateTests
{
    private static readonly DateTimeOffset Start = DomainGen.Now;

    [Fact]
    public void It_is_on_for_thirty_seconds_and_then_off_by_itself()
    {
        var state = TestModeState.StartedAt(Start);

        Timings.TestMode.TestModeDuration.ShouldBe(TimeSpan.FromSeconds(30));
        state.IsOn(Start).ShouldBeTrue();
        state.IsOn(Start.AddSeconds(29.999)).ShouldBeTrue();
        state.IsOn(Start.AddSeconds(30)).ShouldBeFalse();
        TestModeState.Off.IsOn(Start).ShouldBeFalse();
    }

    [Fact]
    public void The_indicator_counts_whole_seconds_down_from_thirty()
    {
        var state = TestModeState.StartedAt(Start);

        state.SecondsLeft(Start).ShouldBe(30);
        state.SecondsLeft(Start.AddMilliseconds(1)).ShouldBe(30);
        state.SecondsLeft(Start.AddSeconds(1)).ShouldBe(29);
        state.SecondsLeft(Start.AddSeconds(29.5)).ShouldBe(1);
        state.SecondsLeft(Start.AddSeconds(30)).ShouldBe(0);
        TestModeState.Off.SecondsLeft(Start).ShouldBe(0);
    }

    [Fact]
    public void The_next_repaint_is_at_the_next_whole_second_or_at_the_end()
    {
        var state = TestModeState.StartedAt(Start);

        state.UntilNextChange(Start).ShouldBe(TimeSpan.FromSeconds(1));
        state.UntilNextChange(Start.AddMilliseconds(300)).ShouldBe(TimeSpan.FromMilliseconds(700));
        state.UntilNextChange(Start.AddSeconds(29.25)).ShouldBe(TimeSpan.FromMilliseconds(750));
        state.UntilNextChange(Start.AddSeconds(30)).ShouldBeNull();
        TestModeState.Off.UntilNextChange(Start).ShouldBeNull();
    }

    [Fact]
    public void Only_short_and_debounced_touches_are_marked_as_ignored()
    {
        TestModeMark.Counted.ShouldBe(new TestModeMark(true, IgnoreReason.None));
        TestModeMark
            .ForIgnored(IgnoreReason.TooShort)
            .ShouldBe(new TestModeMark(false, IgnoreReason.TooShort));
        TestModeMark
            .ForIgnored(IgnoreReason.Debounced)
            .ShouldBe(new TestModeMark(false, IgnoreReason.Debounced));
        foreach (
            var other in new[]
            {
                IgnoreReason.None,
                IgnoreReason.Moved,
                IgnoreReason.Palm,
                IgnoreReason.NoTarget,
                IgnoreReason.SwipeLock,
                IgnoreReason.Canceled,
            }
        )
        {
            TestModeMark.ForIgnored(other).ShouldBeNull();
        }

        Timings.TestMode.TestMarkDuration.ShouldBe(TimeSpan.FromMilliseconds(700));
    }
}
