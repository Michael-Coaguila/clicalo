using Clicalo.Platform.Core.Guardian;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The real pause between two attempts of a refused release (ADR-0018, decision D3): it follows the
/// <see cref="TimeProvider"/>, so it costs no CPU while the session stays locked and the tests drive it with a fake
/// clock.
/// </summary>
[Trait("Req", "SEG-006")]
public sealed class SystemGuardianEnvironmentTests
{
    private static readonly SentinelStartInfo Info = new(
        1,
        2,
        3,
        TimeSpan.FromSeconds(1),
        3,
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(30)
    );

    [Fact]
    public async Task The_pause_before_a_retry_lasts_the_interval_of_the_time_provider()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 22, 0, 0, TimeSpan.Zero));
        var start = time.GetUtcNow();
        var environment = new SystemGuardianEnvironment(Info, time);
        var interval = TimeSpan.FromHours(1);

        // An hour on the fake clock: it only ends if the pause is scheduled on the TimeProvider.
        var pause = Task.Run(() => environment.WaitBeforeRetry(interval), cancellation);
        for (var step = 0; step < 10_000 && !pause.IsCompleted; step++)
        {
            time.Advance(TimeSpan.FromMinutes(1));
            await Task.WhenAny(pause, Task.Delay(TimeSpan.FromMilliseconds(1), cancellation));
        }

        pause.IsCompletedSuccessfully.ShouldBeTrue();
        (time.GetUtcNow() - start).ShouldBeGreaterThanOrEqualTo(interval);
    }
}
