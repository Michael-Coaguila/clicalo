namespace Clicalo.Domain.Migration.V1;

/// <summary>A monitor of this machine, to place the v1 window position (catalog §7.4).</summary>
/// <param name="Id">Device name (<c>\\.\DISPLAY1</c>).</param>
/// <param name="Left">Left edge of its work area, in physical pixels.</param>
/// <param name="Top">Top edge of its work area.</param>
/// <param name="Width">Width of its work area.</param>
/// <param name="Height">Height of its work area.</param>
/// <param name="Scale">DPI scale (1.75 at 175 %), to convert Qt logical pixels.</param>
/// <param name="IsPrimary">Whether it is the primary monitor.</param>
public sealed record V1Monitor(
    string Id,
    int Left,
    int Top,
    int Width,
    int Height,
    double Scale,
    bool IsPrimary
);
