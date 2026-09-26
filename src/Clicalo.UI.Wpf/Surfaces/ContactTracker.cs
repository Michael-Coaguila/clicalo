using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// Follows the contacts of one surface to summarize them for the engine (<see cref="ContactSummary"/>: duration,
/// largest displacement in logical pixels and palm, blueprint §7.8). The gesture recognizer already filtered them
/// (TAC-002); the summary travels with the activation so the single policy of the engine sees the same contact.
/// Runs on the surface's UI thread, without allocating per frame once a contact is known.
/// </summary>
internal sealed class ContactTracker
{
    private readonly Dictionary<uint, Track> _contacts = [];

    /// <summary>The contacts currently down.</summary>
    public int Count => _contacts.Count;

    /// <summary>Records <paramref name="sample"/>: a down starts a contact, a move widens its displacement.</summary>
    public void Observe(in PointerSample sample)
    {
        switch (sample.Phase)
        {
            case PointerPhase.Down:
                _contacts[sample.PointerId] = new Track(
                    sample.Kind,
                    sample.Timestamp,
                    sample.Position,
                    0,
                    Side(sample.Contact)
                );
                break;

            case PointerPhase.Move
            or PointerPhase.Up
            or PointerPhase.Cancel when _contacts.TryGetValue(sample.PointerId, out var track):
                _contacts[sample.PointerId] = track with
                {
                    MaxDistance = Math.Max(
                        track.MaxDistance,
                        track.Start.DistanceTo(sample.Position)
                    ),
                    MaxSide = Math.Max(track.MaxSide, Side(sample.Contact)),
                };
                break;
        }
    }

    /// <summary>Forgets a contact once its last gestures were delivered.</summary>
    public void Forget(in PointerSample sample)
    {
        if (sample.Phase is PointerPhase.Up or PointerPhase.Cancel)
        {
            _contacts.Remove(sample.PointerId);
        }
    }

    /// <summary>Forgets every contact (the surface hid).</summary>
    public void Clear() => _contacts.Clear();

    /// <summary>The device of a contact; a finger when it is unknown.</summary>
    public PointerKind DeviceOf(uint pointerId) =>
        _contacts.TryGetValue(pointerId, out var track) ? track.Kind : PointerKind.Finger;

    /// <summary>The summary of a contact at <paramref name="at"/>, with distances in logical pixels.</summary>
    /// <param name="pointerId">The contact.</param>
    /// <param name="at">When it ended (or now, for a hold that ends without a lift).</param>
    /// <param name="dpiScale">Physical pixels per logical pixel on the surface's monitor.</param>
    public ContactSummary Summarize(uint pointerId, DateTimeOffset at, double dpiScale)
    {
        if (!_contacts.TryGetValue(pointerId, out var track))
        {
            return new ContactSummary(TimeSpan.Zero, 0, PalmLike: false);
        }

        var scale = dpiScale > 0 ? dpiScale : 1;
        var duration = at > track.Down ? at - track.Down : TimeSpan.Zero;
        return new ContactSummary(
            duration,
            track.MaxDistance / scale,
            PalmLike: track.MaxSide / scale >= Timings.Touch.PalmContactMinPx
        );
    }

    private static int Side(PhysicalRect contact) => Math.Max(contact.Width, contact.Height);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Track(
        PointerKind Kind,
        DateTimeOffset Down,
        PhysicalPoint Start,
        double MaxDistance,
        int MaxSide
    );
}
