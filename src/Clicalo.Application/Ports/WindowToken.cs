using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Application.Ports;

/// <summary>
/// An opaque top-level window handle (<c>HWND</c>) as Application sees it (blueprint §3.6). Application never
/// dereferences it: only the adapters of <see cref="IForegroundControl"/>, <see cref="IForegroundMonitor"/> and the
/// windowing of UI.Wpf turn it back into a window. It lives in Ports because every foreground port uses it
/// (deviation D-10).
/// </summary>
/// <param name="Handle">The window handle value; zero is <see cref="None"/>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WindowToken(nint Handle)
{
    /// <summary>No window.</summary>
    public static WindowToken None => default;

    /// <summary>True when the token names no window.</summary>
    public bool IsNone => Handle == 0;

    /// <summary>The handle in hexadecimal, for diagnostics (never a title: titles are sensitive, LOG-001).</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"0x{Handle:X}");
}
