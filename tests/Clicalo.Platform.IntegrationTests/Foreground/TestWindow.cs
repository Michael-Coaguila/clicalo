using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Clicalo.Platform.Windows.SysEvents;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// A small top-level text box of this test process that stands in for a panel surface with a text field: created on a
/// <see cref="SysEventsThread"/> (which pumps its messages), non-activatable (<c>WS_EX_NOACTIVATE</c>) until a lease
/// allows it, shown without activation. Plain Win32 through <c>DllImport</c>: this test project has no CsWin32 of
/// its own, and the generated interop of the referenced assemblies is internal to each of them.
/// </summary>
internal sealed class TestWindow : IAsyncDisposable
{
    private const int ExStyleIndex = -20;
    private const uint NoActivate = 0x0800_0000;
    private const uint Topmost = 0x0000_0008;
    private const uint ToolWindow = 0x0000_0080;
    private const uint Popup = 0x8000_0000;
    private const uint Border = 0x0080_0000;
    private const int ShowNoActivate = 4;
    private const int Left = 120;
    private const int Top = 120;
    private const int Width = 360;
    private const int Height = 48;

    private readonly SysEventsThread _thread;

    private TestWindow(SysEventsThread thread, nint handle)
    {
        _thread = thread;
        Handle = handle;
    }

    /// <summary>The window handle.</summary>
    public nint Handle { get; }

    /// <summary>The handle as the ports see it.</summary>
    public WindowToken Token => new(Handle);

    /// <summary>True while the window carries <c>WS_EX_NOACTIVATE</c>.</summary>
    public bool IsNonActivating => (GetWindowLongW(Handle, ExStyleIndex) & NoActivate) != 0;

    /// <summary>Creates and shows the window on <paramref name="thread"/>, without activating it.</summary>
    public static Task<TestWindow> CreateAsync(SysEventsThread thread) =>
        thread.InvokeAsync(() =>
        {
            var handle = CreateWindowExW(
                NoActivate | Topmost | ToolWindow,
                "EDIT",
                null,
                Popup | Border,
                Left,
                Top,
                Width,
                Height,
                0,
                0,
                0,
                0
            );
            if (handle == 0)
            {
                throw new InvalidOperationException(
                    "CreateWindowEx failed with Win32 error " + Marshal.GetLastPInvokeError() + "."
                );
            }

            _ = ShowWindow(handle, ShowNoActivate);
            return new TestWindow(thread, handle);
        });

    /// <summary>Adds or removes <c>WS_EX_NOACTIVATE</c>, as <c>SurfaceRegistry</c> does for a lease.</summary>
    public void SetNonActivating(bool nonActivating)
    {
        var style = GetWindowLongW(Handle, ExStyleIndex);
        var updated = nonActivating ? style | NoActivate : style & ~NoActivate;
        _ = SetWindowLongW(Handle, ExStyleIndex, updated);
    }

    public async ValueTask DisposeAsync() => await _thread.InvokeAsync(() => DestroyWindow(Handle));

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CreateWindowExW(
        uint exStyle,
        string className,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter
    );

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowLongW(nint window, int index);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint SetWindowLongW(nint window, int index, uint value);
}
