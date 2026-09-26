using Microsoft.Extensions.Time.Testing;

namespace Clicalo.TestKit.Time;

/// <summary>
/// Deterministic clocks for time-dependent code. Product code takes a <see cref="TimeProvider"/> (blueprint §13);
/// tests pass a <see cref="FakeTimeProvider"/> created here and move it explicitly.
/// </summary>
public static class TestTime
{
    /// <summary>
    /// Monday 5 January 2026, 09:00:00 UTC: a fixed and unremarkable instant (not midnight, not the end of a month,
    /// far from any daylight-saving change) so results never depend on when or where the tests run.
    /// </summary>
    public static DateTimeOffset Epoch { get; } = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The smallest step a <see cref="FakeTimeProvider"/> can take: one tick (100 ns).</summary>
    public static TimeSpan Tick { get; } = TimeSpan.FromTicks(1);

    /// <summary>
    /// A fake clock at <paramref name="start"/> (default <see cref="Epoch"/>) whose local time zone is
    /// <paramref name="localTimeZone"/> (default UTC, never the machine's zone). It only moves when told to.
    /// </summary>
    public static FakeTimeProvider CreateProvider(
        DateTimeOffset? start = null,
        TimeZoneInfo? localTimeZone = null
    )
    {
        var provider = new FakeTimeProvider(start ?? Epoch);
        provider.SetLocalTimeZone(localTimeZone ?? TimeZoneInfo.Utc);
        return provider;
    }
}
