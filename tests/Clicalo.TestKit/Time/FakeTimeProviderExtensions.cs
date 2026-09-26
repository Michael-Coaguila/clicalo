using Microsoft.Extensions.Time.Testing;

namespace Clicalo.TestKit.Time;

/// <summary>
/// Moves a <see cref="FakeTimeProvider"/> in the ways threshold tests need. Every timer due on the way fires, in
/// order, on the calling thread.
/// </summary>
/// <example>
/// Boundary test of a named threshold:
/// <code>
/// var deadline = time.After(threshold); // a generated Timings value
/// time.AdvanceToJustBefore(deadline);   // one tick early: nothing yet
/// time.AdvanceTo(deadline);             // exactly on time: it fires
/// </code>
/// </example>
public static class FakeTimeProviderExtensions
{
    /// <summary>The instant <paramref name="delay"/> after the provider's current time.</summary>
    public static DateTimeOffset After(this FakeTimeProvider time, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        return time.GetUtcNow() + delay;
    }

    /// <summary>Advances to <paramref name="instant"/>, which must not be in the provider's past.</summary>
    public static void AdvanceTo(this FakeTimeProvider time, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(time);
        var delta = instant - time.GetUtcNow();
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(instant),
                instant,
                "A fake clock only moves forward; the instant is " + (-delta) + " in its past."
            );
        }

        time.Advance(delta);
    }

    /// <summary>Advances to one <see cref="TestTime.Tick"/> before <paramref name="instant"/>.</summary>
    public static void AdvanceToJustBefore(this FakeTimeProvider time, DateTimeOffset instant) =>
        time.AdvanceTo(instant - TestTime.Tick);

    /// <summary>
    /// Advances by <paramref name="total"/> in steps of <paramref name="step"/> (the last one may be shorter), so
    /// code that samples the clock or re-arms timers from callbacks sees every intermediate instant.
    /// </summary>
    /// <returns>The number of steps taken.</returns>
    public static int AdvanceInSteps(this FakeTimeProvider time, TimeSpan total, TimeSpan step)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThan(total, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, TimeSpan.Zero);

        var steps = 0;
        var remaining = total;
        while (remaining > TimeSpan.Zero)
        {
            var next = remaining < step ? remaining : step;
            time.Advance(next);
            remaining -= next;
            steps++;
        }

        return steps;
    }
}
