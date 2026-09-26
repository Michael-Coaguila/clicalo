using Clicalo.TestKit.Time;

namespace Clicalo.Platform.IntegrationTests.TestKit;

/// <summary>Deterministic clocks (Clicalo.TestKit): the boundary idiom and stepped advancing.</summary>
public sealed class FakeTimeProviderExtensionsTests
{
    private static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(600);

    [Fact]
    public void A_new_provider_starts_at_the_epoch_in_UTC()
    {
        var time = TestTime.CreateProvider();

        time.GetUtcNow().ShouldBe(TestTime.Epoch);
        time.LocalTimeZone.ShouldBe(TimeZoneInfo.Utc);
        TestTime.Epoch.DayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    [Fact]
    public void A_timer_does_not_fire_one_tick_early_and_fires_exactly_on_time()
    {
        var time = TestTime.CreateProvider();
        var fired = 0;
        using var timer = time.CreateTimer(
            _ => fired++,
            state: null,
            Threshold,
            Timeout.InfiniteTimeSpan
        );
        var deadline = time.After(Threshold);

        time.AdvanceToJustBefore(deadline);
        fired.ShouldBe(0);

        time.AdvanceTo(deadline);
        fired.ShouldBe(1);
    }

    [Fact]
    public void Advancing_in_steps_visits_every_intermediate_instant()
    {
        var time = TestTime.CreateProvider();
        var ticks = new List<TimeSpan>();
        var start = time.GetUtcNow();
        using var timer = time.CreateTimer(
            _ => ticks.Add(time.GetUtcNow() - start),
            state: null,
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromMilliseconds(40)
        );

        var steps = time.AdvanceInSteps(
            TimeSpan.FromMilliseconds(130),
            TimeSpan.FromMilliseconds(40)
        );

        steps.ShouldBe(4);
        time.GetUtcNow().ShouldBe(start + TimeSpan.FromMilliseconds(130));
        ticks.ShouldBe([
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromMilliseconds(80),
            TimeSpan.FromMilliseconds(120),
        ]);
    }

    [Fact]
    public void A_fake_clock_never_moves_backwards()
    {
        var time = TestTime.CreateProvider();
        time.Advance(TimeSpan.FromSeconds(1));

        Should.Throw<ArgumentOutOfRangeException>(() => time.AdvanceTo(TestTime.Epoch));
        Should.Throw<ArgumentOutOfRangeException>(() => time.After(TimeSpan.FromTicks(-1)));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            time.AdvanceInSteps(TimeSpan.FromSeconds(1), TimeSpan.Zero)
        );
    }
}
