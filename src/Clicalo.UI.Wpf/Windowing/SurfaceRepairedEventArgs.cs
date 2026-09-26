using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>Data of <see cref="SurfaceIntegrityCheck.Repaired"/>: one property of one surface was put back.</summary>
/// <param name="surface">The repaired surface.</param>
/// <param name="kind">What was repaired.</param>
public sealed class SurfaceRepairedEventArgs(SurfaceId surface, SurfaceRepairKind kind) : EventArgs
{
    /// <summary>The repaired surface.</summary>
    public SurfaceId Surface { get; } = surface;

    /// <summary>What was repaired.</summary>
    public SurfaceRepairKind Kind { get; } = kind;
}
