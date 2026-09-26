namespace Clicalo.Architecture.Tests;

/// <summary>
/// Sources compiled by the slow build tests (<c>EnforcementBuild</c>). Every line that ends with
/// <c>// banned</c> must raise RS0030 and no other line may; <see cref="BannedLines"/> computes the expected lines
/// from the markers.
/// </summary>
internal static class BannedApiProbes
{
    public const string Marker = "// banned";

    /// <summary>A product project named Clicalo.Domain: All and Domain lists.</summary>
    public const string Domain = """
        namespace Clicalo.Domain.Probes;

        /// <summary>Uses banned APIs on purpose.</summary>
        internal static class Probe
        {
            internal static string Clock() => System.DateTime.Now.ToString(System.Globalization.CultureInfo.InvariantCulture); // banned

            internal static string TempPath() => System.IO.Path.GetTempPath(); // banned

            internal static string Machine() => System.Environment.MachineName; // banned

            internal static void Print() => System.Console.WriteLine(); // banned

            internal static System.Guid Id() => System.Guid.NewGuid(); // banned

            internal static int Pure() => System.Math.Max(1, 2);
        }
        """;

    /// <summary>CsWin32 functions whose every generated overload the lists ban.</summary>
    public const string NativeMethods = """
        SendInput
        SetForegroundWindow
        AllowSetForegroundWindow
        AttachThreadInput
        LockSetForegroundWindow
        TrackPopupMenu
        TrackPopupMenuEx
        ShellExecute
        """;

    /// <summary>A product project named Clicalo.UI.Wpf: All and Surfaces lists, CsWin32 and WPF.</summary>
    public const string Surfaces = """
        using System.Runtime.InteropServices;
        using System.Windows;
        using System.Windows.Controls;
        using System.Windows.Controls.Primitives;
        using Windows.Win32;
        using Windows.Win32.Foundation;
        using Windows.Win32.UI.Input.KeyboardAndMouse;
        using Windows.Win32.UI.WindowsAndMessaging;

        namespace Clicalo.UI.Wpf.Probes;

        /// <summary>Uses banned APIs on purpose.</summary>
        internal static unsafe class Probe
        {
            internal static void Run(HWND window, HMENU menu, SafeHandle menuHandle, Window host)
            {
                Span<INPUT> inputs = stackalloc INPUT[1];
                _ = PInvoke.SendInput(inputs, sizeof(INPUT)); // banned
                fixed (INPUT* pointer = inputs)
                {
                    _ = PInvoke.SendInput(1, pointer, sizeof(INPUT)); // banned
                }

                _ = PInvoke.SetForegroundWindow(window); // banned
                _ = PInvoke.AllowSetForegroundWindow(0); // banned
                _ = PInvoke.AttachThreadInput(0, 0, false); // banned
                _ = PInvoke.LockSetForegroundWindow(FOREGROUND_WINDOW_LOCK_CODE.LSFW_LOCK); // banned
                _ = PInvoke.TrackPopupMenu(menu, 0, 0, 0, 0, window, null); // banned
                _ = PInvoke.TrackPopupMenu(menuHandle, 0, 0, 0, window, null); // banned
                _ = PInvoke.TrackPopupMenuEx(menu, 0, 0, 0, window, null); // banned
                _ = PInvoke.TrackPopupMenuEx(menuHandle, 0, 0, 0, window, null); // banned
                using var library = PInvoke.ShellExecute(window, "open", "notepad.exe", null, null, SHOW_WINDOW_CMD.SW_SHOWNORMAL); // banned
                _ = PInvoke.ShellExecute(window, default(PCWSTR), default(PCWSTR), default(PCWSTR), default(PCWSTR), SHOW_WINDOW_CMD.SW_SHOWNORMAL); // banned
                _ = host.Activate(); // banned
                _ = host.Focus(); // banned
                _ = new ComboBox(); // banned
                _ = new Popup(); // banned
                _ = new ContextMenu(); // banned
                _ = new ToolTip(); // banned
                _ = System.DateTime.UtcNow; // banned
                _ = host.IsActive;
            }
        }
        """;

    /// <summary>A tool project: the lists do not apply outside src/.</summary>
    public const string Tool = """
        namespace Clicalo.DevCli.Probes;

        /// <summary>Uses APIs that are banned in product code.</summary>
        internal static class Probe
        {
            internal static string Now() => System.DateTime.Now.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        """;

    /// <summary>1-based numbers of the lines marked <c>// banned</c>.</summary>
    public static int[] BannedLines(string source) =>
        [
            .. source
                .ReplaceLineEndings("\n")
                .Split('\n')
                .Select((line, index) => (line, number: index + 1))
                .Where(pair => pair.line.TrimEnd().EndsWith(Marker, StringComparison.Ordinal))
                .Select(pair => pair.number),
        ];
}
