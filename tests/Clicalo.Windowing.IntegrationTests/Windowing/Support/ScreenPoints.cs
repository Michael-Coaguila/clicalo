using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// What is at a screen point, read from outside: the window that hit testing gives a click or a touch there
/// (<c>WindowFromPoint</c>, which honors per-pixel alpha, <c>WS_EX_TRANSPARENT</c> and window regions) and the color
/// DWM composed there. Test-only (spike S6).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ScreenPoints
{
    /// <summary>The window a touch at (<paramref name="x"/>, <paramref name="y"/>) would reach.</summary>
    public static nint WindowAt(int x, int y) =>
        WindowFromPoint(new NativeSurface.Point { X = x, Y = y });

    /// <summary>The composed color at (<paramref name="x"/>, <paramref name="y"/>) as (R, G, B).</summary>
    public static (int R, int G, int B) ColorAt(int x, int y)
    {
        var screen = GetDC(0);
        try
        {
            var color = GetPixel(screen, x, y);
            return ((int)(color & 0xFF), (int)((color >> 8) & 0xFF), (int)((color >> 16) & 0xFF));
        }
        finally
        {
            _ = ReleaseDC(0, screen);
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint WindowFromPoint(NativeSurface.Point point);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetPixel(nint deviceContext, int x, int y);
}
