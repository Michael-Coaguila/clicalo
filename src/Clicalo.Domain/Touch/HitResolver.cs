using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.Touch;

/// <summary>
/// Chooses the target of a contact (blueprint §7.1 <c>HitResolver</c>, REG-02, TAC-002):
/// <list type="number">
/// <item>a target whose bounds contain the point wins; if the bounds of several overlap (controls drawn smaller than
/// 44 and enlarged to 44), the one whose center is nearest wins (REG-02);</item>
/// <item>otherwise, among the targets whose extra hit area contains the point, the nearest one (distance to its
/// bounds) wins, and a tie goes to the nearest center (TAC-002);</item>
/// <item>a remaining tie goes to the target listed first, so the result never depends on anything else.</item>
/// </list>
/// Empty targets are never hit.
/// </summary>
internal static class HitResolver
{
    /// <summary>The index of the target hit at <paramref name="point"/>, or -1 when there is none.</summary>
    /// <param name="targets">The targets, in physical screen pixels.</param>
    /// <param name="point">The contact position, in physical screen pixels.</param>
    /// <param name="hitSlop">The extra hit area, in physical pixels.</param>
    public static int Resolve(ReadOnlySpan<TouchTarget> targets, PhysicalPoint point, int hitSlop)
    {
        var best = -1;
        var bestInside = false;
        var bestEdge = 0L;
        var bestCenter = 0d;
        for (var i = 0; i < targets.Length; i++)
        {
            var bounds = targets[i].Bounds;
            if (bounds.IsEmpty)
            {
                continue;
            }

            var inside = bounds.Contains(point);
            if (!inside && !bounds.Inflate(hitSlop).Contains(point))
            {
                continue;
            }

            var edge = inside ? 0L : EdgeDistanceSquared(bounds, point);
            var center = CenterDistanceSquared(bounds, point);
            if (best < 0 || IsBetter(inside, edge, center, bestInside, bestEdge, bestCenter))
            {
                best = i;
                bestInside = inside;
                bestEdge = edge;
                bestCenter = center;
            }
        }

        return best;
    }

    /// <summary>
    /// Squared distance from <paramref name="point"/> to the nearest pixel of <paramref name="bounds"/> (zero inside).
    /// </summary>
    public static long EdgeDistanceSquared(PhysicalRect bounds, PhysicalPoint point)
    {
        long dx =
            point.X < bounds.Left ? (long)bounds.Left - point.X
            : point.X >= bounds.Right ? (long)point.X - (bounds.Right - 1L)
            : 0L;
        long dy =
            point.Y < bounds.Top ? (long)bounds.Top - point.Y
            : point.Y >= bounds.Bottom ? (long)point.Y - (bounds.Bottom - 1L)
            : 0L;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>Squared distance from <paramref name="point"/> to the exact center of <paramref name="bounds"/>.</summary>
    public static double CenterDistanceSquared(PhysicalRect bounds, PhysicalPoint point)
    {
        var dx = point.X - (bounds.Left + (bounds.Width / 2d));
        var dy = point.Y - (bounds.Top + (bounds.Height / 2d));
        return (dx * dx) + (dy * dy);
    }

    private static bool IsBetter(
        bool inside,
        long edge,
        double center,
        bool bestInside,
        long bestEdge,
        double bestCenter
    )
    {
        if (inside != bestInside)
        {
            return inside;
        }

        if (!inside && edge != bestEdge)
        {
            return edge < bestEdge;
        }

        return center < bestCenter;
    }
}
