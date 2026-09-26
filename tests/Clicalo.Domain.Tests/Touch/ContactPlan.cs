using System.Collections.Immutable;
using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>What one generated contact does: where and when it goes down, its moves and how it ends.</summary>
/// <param name="PointerId">Its identifier, unique in the trace.</param>
/// <param name="StartMs">When it goes down, in milliseconds from the trace start.</param>
/// <param name="Start">Where it goes down.</param>
/// <param name="Moves">Each move: the delay since the previous event and the displacement.</param>
/// <param name="EndDelayMs">Delay between the last move and the end.</param>
/// <param name="Canceled">True when the system cancels it instead of a lift.</param>
/// <param name="ContactSize">Side of its contact area, in physical pixels (a palm when large).</param>
internal sealed record ContactPlan(
    uint PointerId,
    int StartMs,
    PhysicalPoint Start,
    ImmutableArray<(int DelayMs, int Dx, int Dy)> Moves,
    int EndDelayMs,
    bool Canceled,
    int ContactSize
)
{
    /// <summary>When the contact ends, in milliseconds from the trace start.</summary>
    public int EndMs => StartMs + Moves.Sum(m => m.DelayMs) + EndDelayMs;
}
