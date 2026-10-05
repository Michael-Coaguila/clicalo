using System.Runtime.InteropServices;

namespace Clicalo.Domain.Dimming;

/// <summary>What <see cref="DimPolicy"/> decides for one surface.</summary>
/// <param name="TargetOpacity">The opacity the surface goes to, 0 to 1.</param>
/// <param name="Transition">How long the change of opacity lasts; zero with reduce motion (TEM-006).</param>
/// <param name="Dimmed">Whether the surface is dimmed.</param>
/// <param name="NextEvaluationAt">When to evaluate again because the surface will dim then; null when nothing is due.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DimDecision(
    double TargetOpacity,
    TimeSpan Transition,
    bool Dimmed,
    DateTimeOffset? NextEvaluationAt
);
