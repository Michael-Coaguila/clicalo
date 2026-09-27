using System.Diagnostics;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// Pen and touch arbitration: on a machine with a pen, Windows ignores touch while a pen is in range and for a short
/// time after it leaves, so a synthetic finger that goes down a few milliseconds after a synthetic pen went out of range
/// never reaches any window (no <c>WM_POINTERDOWN</c>, no <c>WM_POINTERUP</c>). A person never touches that fast after
/// putting the pen down, but consecutive desktop tests do: <c>NonActivationTests</c> failed its first finger tap right
/// after the pen case on the maintainer's machine (a pen-capable touch screen), while the same tap passed with the
/// ~450 ms <c>PrepareAsync</c> pause of the pointer tests. <see cref="SyntheticPointer"/> therefore lets
/// <see cref="Window"/> pass since the last pen left before a finger goes down. The finger still has to arrive: nothing
/// is retried or skipped.
/// </summary>
internal sealed class PenTouchSettle
{
    /// <summary>
    /// How long a finger waits after the last pen left the detection range: above the ~450 ms after which the finger
    /// arrived in the pointer tests, with a margin.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private long _penLeftAt;

    /// <summary>The arbitration of this process: every <see cref="SyntheticPointer"/> shares it.</summary>
    public static PenTouchSettle Shared { get; } = new();

    /// <summary>Records that a pen left the detection range at <paramref name="timestamp"/> (<see cref="Stopwatch"/>).</summary>
    public void PenLeft(long timestamp) => Volatile.Write(ref _penLeftAt, timestamp);

    /// <summary>
    /// How long a finger that goes down at <paramref name="now"/> (<see cref="Stopwatch"/>) still has to wait; zero
    /// when no pen was used or it left at least <see cref="Window"/> ago.
    /// </summary>
    public TimeSpan RemainingBeforeTouch(long now)
    {
        var left = Volatile.Read(ref _penLeftAt);
        if (left == 0)
        {
            return TimeSpan.Zero;
        }

        var elapsed = Stopwatch.GetElapsedTime(left, now);
        return elapsed >= Window ? TimeSpan.Zero : Window - elapsed;
    }
}
