using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Touch;

/// <summary>
/// Test mode (TAC-008): on for <c>Timings.TestMode.TestModeDuration</c> (30 s) from the moment it is switched on, then
/// off by itself. While on, every touch passes the filter of TAC-002 and is only marked; nothing is sent (INV-7, with
/// the engine's own test mode flag). Immutable: the panel replaces it.
/// </summary>
/// <param name="Until">When it switches itself off; <see langword="null"/> while off.</param>
public sealed record TestModeState(DateTimeOffset? Until)
{
    /// <summary>Test mode off.</summary>
    public static TestModeState Off { get; } = new((DateTimeOffset?)null);

    /// <summary>Test mode switched on at <paramref name="now"/>.</summary>
    /// <param name="now">When it is switched on.</param>
    public static TestModeState StartedAt(DateTimeOffset now) =>
        new(now + Timings.TestMode.TestModeDuration);

    /// <summary>Whether it is on at <paramref name="now"/> (the end is excluded).</summary>
    /// <param name="now">The current time.</param>
    public bool IsOn(DateTimeOffset now) => Until is { } until && now < until;

    /// <summary>
    /// The whole seconds left, rounded up, for the indicator «Modo prueba · {s} s» (30 right after switching it on,
    /// 1 during its last second); 0 when off.
    /// </summary>
    /// <param name="now">The current time.</param>
    public int SecondsLeft(DateTimeOffset now) =>
        Until is { } until && now < until ? (int)CeilingSeconds(until - now) : 0;

    /// <summary>
    /// How long until <see cref="SecondsLeft"/> changes (the next whole second, or the end); <see langword="null"/>
    /// when off. The panel waits this long before it repaints the indicator.
    /// </summary>
    /// <param name="now">The current time.</param>
    public TimeSpan? UntilNextChange(DateTimeOffset now)
    {
        if (Until is not { } until || now >= until)
        {
            return null;
        }

        var left = until - now;
        return TimeSpan.FromTicks(
            left.Ticks - ((CeilingSeconds(left) - 1) * TimeSpan.TicksPerSecond)
        );
    }

    private static long CeilingSeconds(TimeSpan left) =>
        (left.Ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond;
}
