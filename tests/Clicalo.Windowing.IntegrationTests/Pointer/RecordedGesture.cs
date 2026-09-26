using Clicalo.Domain.Touch;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>A gesture as the surface received it.</summary>
/// <param name="Gesture">The gesture.</param>
/// <param name="ReceivedAt">When the surface received it, on the source's clock.</param>
/// <param name="ThreadId">The managed thread that delivered it.</param>
public sealed record RecordedGesture(GestureEvent Gesture, DateTimeOffset ReceivedAt, int ThreadId)
{
    /// <summary>From the moment Windows recorded the input (the frame stamp) to the surface receiving the gesture.</summary>
    public TimeSpan Latency => ReceivedAt - Gesture.Timestamp;
}
