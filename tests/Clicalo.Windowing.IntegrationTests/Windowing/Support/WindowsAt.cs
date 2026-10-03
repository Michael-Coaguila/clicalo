using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// The visible top-level windows whose rectangle contains a screen point, from the top of the z-order down, with their
/// class and process (never their title): who would receive a touch that falls through. Test diagnostics only.
/// </summary>
internal static class WindowsAt
{
    private const int MaxWindows = 6;
    private const int ExStyleIndex = -20;
    private const uint ExTopmost = 0x0000_0008;
    private const uint ExNoActivate = 0x0800_0000;
    private const uint ExTransparent = 0x0000_0020;
    private const uint ExLayered = 0x0008_0000;

    private delegate bool EnumWindowsProc(nint window, nint parameter);

    /// <summary>Up to six windows under (<paramref name="x"/>, <paramref name="y"/>), topmost first.</summary>
    public static IReadOnlyList<string> Describe(int x, int y)
    {
        var found = new List<string>();
        _ = EnumWindows(
            (window, _) =>
            {
                if (
                    IsWindowVisible(window)
                    && NativeSurface.GetWindowRect(window, out var bounds)
                    && x >= bounds.Left
                    && x < bounds.Right
                    && y >= bounds.Top
                    && y < bounds.Bottom
                )
                {
                    found.Add(Line(window, bounds));
                }

                return found.Count < MaxWindows;
            },
            0
        );
        return found;
    }

    private static string Line(nint window, NativeSurface.Rect bounds)
    {
        var buffer = new char[256];
        var length = GetClassNameW(window, buffer, buffer.Length);
        var className = new string(buffer, 0, Math.Max(0, length));
        _ = GetWindowThreadProcessId(window, out var processId);
        var style = NativeSurface.ExStyle(window);
        var flags = string.Concat(
            (style & ExTopmost) != 0 ? " topmost" : "",
            (style & ExNoActivate) != 0 ? " noactivate" : "",
            (style & ExTransparent) != 0 ? " transparent" : "",
            (style & ExLayered) != 0 ? " layered" : "",
            IsCloaked(window) ? " cloaked" : ""
        );
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{window:X} {className} of {ProcessName(processId)} (pid {processId}) ({bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom}){flags}"
        );
    }

    private static bool IsCloaked(nint window) =>
        DwmGetWindowAttribute(window, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static string ProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return "an exited process";
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetClassNameW(nint window, [Out] char[] name, int capacity);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmGetWindowAttribute(
        nint window,
        uint attribute,
        out int value,
        int size
    );
}
