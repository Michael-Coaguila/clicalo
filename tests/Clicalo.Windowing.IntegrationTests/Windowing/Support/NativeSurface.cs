using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// The user32 functions the windowing tests use to look at a surface from outside, independently of the product's
/// own CsWin32 bindings (which are internal to UI.Wpf). Test-only.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeSurface
{
    public const int ExStyleIndex = -20;
    public const uint ExNoActivate = 0x0800_0000;
    public const uint ExTopmost = 0x0000_0008;
    public const uint ExToolWindow = 0x0000_0080;
    public const uint ExAppWindow = 0x0004_0000;

    public const uint Owner = 4;
    public const uint NextInZOrder = 2;

    public const uint NoSize = 0x0001;
    public const uint NoMove = 0x0002;
    public const uint NoZOrder = 0x0004;
    public const uint NoActivate = 0x0010;
    public const uint NoOwnerZOrder = 0x0200;

    public const nint TopmostBand = -1;
    public const nint NotTopmostBand = -2;

    public const uint WmActivate = 0x0006;
    public const uint WmSetFocus = 0x0007;
    public const uint WmActivateApp = 0x001C;
    public const uint WmShowWindow = 0x0018;
    public const uint WmMouseActivate = 0x0021;
    public const uint WmWindowPosChanging = 0x0046;
    public const uint WmDisplayChange = 0x007E;
    public const uint WmNcHitTest = 0x0084;
    public const uint WmNcActivate = 0x0086;
    public const uint WmLeftButtonDown = 0x0201;
    public const uint WmLeftButtonUp = 0x0202;
    public const uint WmPointerDown = 0x0246;
    public const uint WmPointerUp = 0x0247;
    public const uint WmPointerActivate = 0x024B;
    public const uint WmDpiChanged = 0x02E0;
    public const uint WmGetDpiScaledSize = 0x02E4;

    public const nint Active = 1;
    public const nint Inactive = 0;

    public const nint MouseNoActivate = 3;
    public const nint PointerNoActivate = 3;
    public const nint HitNowhere = 0;
    public const nint HitClient = 1;

    private const uint MonitorPrimary = 1;

    /// <summary>The extended style of <paramref name="window"/>.</summary>
    public static uint ExStyle(nint window) =>
        unchecked((uint)GetWindowLongW(window, ExStyleIndex));

    /// <summary>Sets the extended style of <paramref name="window"/> from outside the product.</summary>
    public static void SetExStyle(nint window, uint style) =>
        _ = SetWindowLongW(window, ExStyleIndex, unchecked((int)style));

    /// <summary>True when every bit of <paramref name="bits"/> is set in the extended style.</summary>
    public static bool HasExStyle(nint window, uint bits) => (ExStyle(window) & bits) == bits;

    /// <summary>The window rectangle, in physical pixels (the test process is per-monitor aware).</summary>
    public static Rect Bounds(nint window) =>
        GetWindowRect(window, out var bounds) ? bounds : default;

    /// <summary>True when <paramref name="upper"/> comes before <paramref name="lower"/> in the z-order.</summary>
    public static bool IsAbove(nint upper, nint lower)
    {
        var current = GetWindow(upper, NextInZOrder);
        for (var steps = 0; current != 0 && steps < 100_000; steps++)
        {
            if (current == lower)
            {
                return true;
            }

            current = GetWindow(current, NextInZOrder);
        }

        return false;
    }

    /// <summary>The work area of the primary monitor, in physical pixels, and its effective DPI.</summary>
    public static (Rect WorkArea, uint Dpi) PrimaryWorkArea()
    {
        var monitor = MonitorFromPoint(default, MonitorPrimary);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info))
        {
            throw new InvalidOperationException("GetMonitorInfo failed for the primary monitor.");
        }

        var dpi = GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 ? dpiX : 96u;
        return (info.Work, dpi);
    }

    /// <summary>
    /// Sends <paramref name="message"/> with a structure as <c>lParam</c> and returns the result and the structure as
    /// the window left it.
    /// </summary>
    public static (nint Result, T After) SendWithStructure<T>(
        nint window,
        uint message,
        nint wParam,
        T value
    )
        where T : struct
    {
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        try
        {
            Marshal.StructureToPtr(value, buffer, fDeleteOld: false);
            var result = SendMessageW(window, message, wParam, buffer);
            return (result, Marshal.PtrToStructure<T>(buffer));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>A 32-bit <c>lParam</c> made of two signed 16-bit coordinates.</summary>
    public static nint PointParameter(int x, int y) => (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int GetWindowLongW(nint window, int index);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int SetWindowLongW(nint window, int index, int value);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags
    );

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint window, out Rect bounds);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern nint SendMessageW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [DllImport("shcore.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetDpiForMonitor(
        nint monitor,
        int type,
        out uint dpiX,
        out uint dpiY
    );

    /// <summary>Win32 <c>RECT</c> (right and bottom exclusive).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;

        public readonly int CenterX => Left + (Width / 2);

        public readonly int CenterY => Top + (Height / 2);
    }

    /// <summary>Win32 <c>POINT</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>Win32 <c>SIZE</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Size
    {
        public int Width;
        public int Height;
    }

    /// <summary>Win32 <c>WINDOWPOS</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPosition
    {
        public nint Window;
        public nint InsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    /// <summary>Win32 <c>MONITORINFO</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
