using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>The identities of the surfaces of M2 in <c>SurfaceRegistry</c>.</summary>
public static class PanelSurfaceIds
{
    /// <summary>The panel (the only surface of the walking skeleton).</summary>
    public static SurfaceId Panel { get; } = new(SurfaceKind.Panel, 0);
}
