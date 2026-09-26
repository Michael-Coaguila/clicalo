using Clicalo.Application.Ports;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>The identities of the laboratory surfaces (blueprint §8.1 kinds).</summary>
internal static class LabSurfaceIds
{
    /// <summary>The panel.</summary>
    public static SurfaceId Panel { get; } = new(SurfaceKind.Panel, 0);

    /// <summary>The edge bar («Pestaña») with its handle tile.</summary>
    public static SurfaceId Dock { get; } = new(SurfaceKind.Dock, 0);

    /// <summary>The side window of the edge bar.</summary>
    public static SurfaceId DockSide { get; } = new(SurfaceKind.SideWindow, 1);

    /// <summary>The search window (the target of the <c>TextInput</c> lease).</summary>
    public static SurfaceId Search { get; } = new(SurfaceKind.SideWindow, 2);

    /// <summary>The profile side window opened by «Perfil».</summary>
    public static SurfaceId Profiles { get; } = new(SurfaceKind.SideWindow, 3);

    /// <summary>The 64 px bubble.</summary>
    public static SurfaceId Bubble { get; } = new(SurfaceKind.Bubble, 0);

    /// <summary>The guide strip: a floating notice of the laboratory.</summary>
    public static SurfaceId Guide { get; } = new(SurfaceKind.Notice, 9);
}
