using Clicalo.Domain.Touch;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>A frame as the surface received it, with its samples copied out of the source's reused buffer.</summary>
/// <param name="Frame">The frame.</param>
/// <param name="ReceivedAt">When the sink received it, on the source's clock.</param>
public sealed record RecordedFrame(PointerFrame Frame, DateTimeOffset ReceivedAt);
