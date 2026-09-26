using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Clicalo.Domain.Touch;

/// <summary>
/// Every contact of one pointer frame of one surface (<c>GetPointerFrameTouchInfo</c>): the unit that
/// <c>PointerInputSource</c> publishes and <see cref="GestureRecognizer.Feed"/> consumes (blueprint §7.8). A pen or
/// mouse message is a frame with one sample.
/// </summary>
/// <param name="FrameId">Frame identifier (<c>POINTER_INFO.frameId</c>), shared by the samples of one frame.</param>
/// <param name="Timestamp">When the frame happened, on the recognizer's <see cref="TimeProvider"/> timeline.</param>
/// <param name="Samples">The contacts of the frame, at least one, ordered by <see cref="PointerSample.PointerId"/>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PointerFrame(
    uint FrameId,
    DateTimeOffset Timestamp,
    ImmutableArray<PointerSample> Samples
);
