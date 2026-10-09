using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Touch;

/// <summary>
/// The drag of one zone that moves a surface (PAN-004, PES-002, BUR-001): the grip and the title of the panel, the
/// bubble and the handle of the Tab. It follows the first contact that went down on the zone; once that contact has
/// moved more than the drag threshold, max(<c>Timings.Touch.DragMinDistancePx</c>, cancel distance), it is a drag:
/// every move reports how far it is from where it went down, and when it lifts it does <b>not</b> count as a tap. A
/// contact that lifts before the threshold is a tap of the zone. Pure and used from the surface's thread only.
/// </summary>
public sealed class DragTracker
{
    private double _threshold;

    /// <summary>Creates a tracker with the drag threshold of <paramref name="settings"/> at <paramref name="dpiScale"/>.</summary>
    /// <param name="settings">The touch filter values (logical pixels).</param>
    /// <param name="dpiScale">Physical pixels per logical pixel.</param>
    public DragTracker(TouchSettings settings, double dpiScale) => Configure(settings, dpiScale);

    /// <summary>Whether a contact is being followed.</summary>
    public bool IsTracking { get; private set; }

    /// <summary>Whether the followed contact passed the threshold and drags.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>The contact being followed.</summary>
    public uint PointerId { get; private set; }

    /// <summary>Where the followed contact went down, in physical pixels.</summary>
    public PhysicalPoint Origin { get; private set; }

    /// <summary>The drag threshold, in physical pixels.</summary>
    public double ThresholdPx => _threshold;

    /// <summary>The threshold of a drag (PAN-004): max(<c>DragMinDistancePx</c>, cancel distance), in physical pixels.</summary>
    /// <param name="settings">The touch filter values (logical pixels).</param>
    /// <param name="dpiScale">Physical pixels per logical pixel.</param>
    public static double Threshold(TouchSettings settings, double dpiScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiScale);
        return Math.Max(Timings.Touch.DragMinDistancePx, settings.CancelMovePx) * dpiScale;
    }

    /// <summary>Takes new touch values or a new scale (the surface moved to another monitor).</summary>
    /// <param name="settings">The touch filter values.</param>
    /// <param name="dpiScale">Physical pixels per logical pixel.</param>
    public void Configure(TouchSettings settings, double dpiScale) =>
        _threshold = Threshold(settings, dpiScale);

    /// <summary>A contact went down on the zone; ignored while another one is followed.</summary>
    /// <param name="pointerId">The contact.</param>
    /// <param name="at">Where, in physical pixels.</param>
    /// <returns>Whether this contact is now followed.</returns>
    public bool Down(uint pointerId, PhysicalPoint at)
    {
        if (IsTracking)
        {
            return false;
        }

        IsTracking = true;
        IsDragging = false;
        PointerId = pointerId;
        Origin = at;
        return true;
    }

    /// <summary>The followed contact moved.</summary>
    /// <param name="pointerId">The contact.</param>
    /// <param name="at">Where, in physical pixels.</param>
    /// <returns>
    /// The offset from where it went down while it drags (the first one when it passes the threshold);
    /// <see langword="null"/> for another contact or before the threshold.
    /// </returns>
    public PhysicalOffset? Move(uint pointerId, PhysicalPoint at)
    {
        if (!IsTracking || pointerId != PointerId)
        {
            return null;
        }

        if (!IsDragging && Origin.DistanceTo(at) > _threshold)
        {
            IsDragging = true;
        }

        return IsDragging ? new PhysicalOffset(at.X - Origin.X, at.Y - Origin.Y) : null;
    }

    /// <summary>The followed contact lifted or was cancelled: the tracker forgets it.</summary>
    /// <param name="pointerId">The contact.</param>
    /// <returns>
    /// <see cref="DragEnd.Dragged"/> after a drag, <see cref="DragEnd.Tapped"/> before the threshold and
    /// <see cref="DragEnd.None"/> for another contact.
    /// </returns>
    public DragEnd Up(uint pointerId)
    {
        if (!IsTracking || pointerId != PointerId)
        {
            return DragEnd.None;
        }

        var end = IsDragging ? DragEnd.Dragged : DragEnd.Tapped;
        Reset();
        return end;
    }

    /// <summary>Forgets the followed contact (the surface hid or the system cancelled the contact).</summary>
    public void Reset()
    {
        IsTracking = false;
        IsDragging = false;
        PointerId = 0;
        Origin = default;
    }
}
