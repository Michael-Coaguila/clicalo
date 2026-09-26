namespace Clicalo.UI.Wpf.Windowing;

/// <summary>What <see cref="SurfaceIntegrityCheck"/> found drifted on a surface and put back.</summary>
public enum SurfaceRepairKind
{
    /// <summary><c>WS_EX_NOACTIVATE</c> was missing outside an activation lease.</summary>
    NoActivateStyle,

    /// <summary>The surface had left the topmost band (<c>WS_EX_TOPMOST</c> missing).</summary>
    Topmost,
}
