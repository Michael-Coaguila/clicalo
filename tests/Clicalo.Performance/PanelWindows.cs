using System.Runtime.InteropServices;

namespace Clicalo.Performance;

/// <summary>
/// Finds the panel of a running <c>Clicalo.exe</c> from outside the process (the measurements never reference the app):
/// its visible top-level window titled with the app name, its bounds and its DPI. Read-only Win32 calls.
/// </summary>
internal static partial class PanelWindows
{
    /// <summary>The panel's title (<c>appName</c> of data/i18n, the same in Spanish and English).</summary>
    public const string Title = "Clícalo";

    /// <summary>
    /// Logical offset of the center of the first tile from the panel's top-left corner, for size M (sizes.json: tile
    /// 92 × 78, gap 8): border 1, grid margin 4, tile margin 4, half a tile. The recognizer's extra hit area (at least
    /// 8 px) absorbs a pixel of rounding.
    /// </summary>
    public static readonly (double X, double Y) FirstTileCenter = (1 + 4 + 4 + 46, 1 + 4 + 4 + 39);

    private delegate bool EnumWindowsProc(nint window, nint parameter);

    /// <summary>The panel of process <paramref name="processId"/>, or <see langword="null"/>.</summary>
    public static PanelWindow? Find(int processId)
    {
        PanelWindow? found = null;
        _ = EnumWindows(
            (window, parameter) =>
            {
                _ = GetWindowThreadProcessId(window, out var owner);
                if (
                    owner != (uint)processId
                    || !IsWindowVisible(window)
                    || !HasTitle(window, Title)
                )
                {
                    return true;
                }

                if (!GetWindowRect(window, out var rect) || rect.Right <= rect.Left)
                {
                    return true;
                }

                found = new PanelWindow(
                    window,
                    rect.Left,
                    rect.Top,
                    rect.Right,
                    rect.Bottom,
                    GetDpiForWindow(window)
                );
                return false;
            },
            0
        );
        return found;
    }

    private static bool HasTitle(nint window, string title)
    {
        var buffer = new char[64];
        var length = GetWindowTextW(window, buffer, buffer.Length);
        return length > 0 && new string(buffer, 0, length).Equals(title, StringComparison.Ordinal);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint window, [Out] char[] text, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>A panel found on screen, in physical pixels.</summary>
    public sealed record PanelWindow(
        nint Handle,
        int Left,
        int Top,
        int Right,
        int Bottom,
        uint Dpi
    )
    {
        /// <summary>The center of the first tile, in physical pixels.</summary>
        public (int X, int Y) FirstTile
        {
            get
            {
                var scale = Dpi / 96.0;
                return (
                    Left + (int)Math.Round(FirstTileCenter.X * scale),
                    Top + (int)Math.Round(FirstTileCenter.Y * scale)
                );
            }
        }
    }
}
