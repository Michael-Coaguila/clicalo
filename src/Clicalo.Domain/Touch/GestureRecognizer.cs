using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Touch;

/// <summary>
/// Turns the pointer frames of ONE surface into gestures on its targets (blueprint §7.8, ADR-0006): hit resolution
/// with the extra area and nearest center, the filter of TAC-002 (<see cref="TouchFilter"/>), long press, hold,
/// swipe and ignored contacts with their reason. Pure: it never reads a clock (the frames carry their timestamps and
/// <see cref="OnTick"/> receives the time), never allocates on the hot path (the caller owns the output list) and is
/// used only from the thread of its surface.
/// </summary>
/// <remarks>
/// Thresholds come from <c>Clicalo.Domain.Timing.Timings.Touch</c> (NFR-020): <c>LongPress</c> 600 ms,
/// <c>SwipeMinDistancePx</c> 60, <c>SwipeMaxSlope</c> 0.6, <c>PostSwipeLock</c> 300 ms and the drag threshold
/// max(<c>DragMinDistancePx</c>, cancel distance). Logical pixels are scaled with <see cref="DpiScale"/>.
/// </remarks>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the pointer package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class GestureRecognizer
{
    /// <summary>Creates a recognizer for a surface with the given filter values and DPI scale.</summary>
    /// <param name="settings">Active filter values (logical pixels).</param>
    /// <param name="dpiScale">Physical pixels per logical pixel of the surface's monitor (1.0 at 96 DPI).</param>
    public GestureRecognizer(TouchSettings settings, double dpiScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiScale);
        Settings = settings;
        DpiScale = dpiScale;
    }

    /// <summary>Active filter values, in logical pixels.</summary>
    public TouchSettings Settings { get; private set; }

    /// <summary>Physical pixels per logical pixel of the surface's current monitor.</summary>
    public double DpiScale { get; private set; }

    /// <summary>The targets hit-tested by <see cref="Feed"/>, in physical screen pixels.</summary>
    public ImmutableArray<TouchTarget> Targets { get; private set; } = [];

    /// <summary>Contacts currently down on the surface (drives the frozen layout of PAN-009).</summary>
    public int ActiveContacts => throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// The next time <see cref="OnTick"/> must run for a pending long press, hold start or swipe lock; null when
    /// nothing is pending. The surface schedules one <see cref="TimeProvider"/> timer for it.
    /// </summary>
    public DateTimeOffset? NextDeadline => throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// Replaces the targets after a layout change. Contacts already down keep the target they started on (the
    /// layout is frozen under the finger, PAN-009).
    /// </summary>
    public void SetTargets(ImmutableArray<TouchTarget> targets) =>
        throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// Applies new filter values or a new DPI scale (the surface moved to another monitor). Contacts already down
    /// finish with the old values.
    /// </summary>
    public void Configure(TouchSettings settings, double dpiScale) =>
        throw new NotImplementedException("M1 pointer package.");

    /// <summary>Feeds one frame and appends the gestures it completes to <paramref name="output"/>.</summary>
    /// <param name="frame">Every contact of one pointer frame, in time order with the previous frames.</param>
    /// <param name="output">Receives the gestures, in order; never cleared by the recognizer.</param>
    public void Feed(in PointerFrame frame, ICollection<GestureEvent> output) =>
        throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// Advances time without input and appends the gestures that became due (a long press, a hold start after the
    /// minimum contact) to <paramref name="output"/>.
    /// </summary>
    /// <param name="now">The current time, on the same timeline as the frames.</param>
    /// <param name="output">Receives the gestures, in order.</param>
    public void OnTick(DateTimeOffset now, ICollection<GestureEvent> output) =>
        throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// Forgets every contact (surface hidden, session locked): each active hold ends with
    /// <see cref="HoldEndReason.Reset"/> in <paramref name="output"/> so no key stays down (REG-03).
    /// </summary>
    public void Reset(DateTimeOffset now, ICollection<GestureEvent> output) =>
        throw new NotImplementedException("M1 pointer package.");
}
