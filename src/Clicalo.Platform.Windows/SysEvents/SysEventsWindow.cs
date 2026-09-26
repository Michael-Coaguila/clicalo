using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// A window owned by a <see cref="SysEventsThread"/>: its message-only window, or a hidden top-level helper (the tray
/// callback window, <c>TrayMenuHost</c>). Messages are routed to handlers registered per message number; everything
/// else goes to <c>DefWindowProc</c>. Created, used and destroyed on the SysEvents thread only.
/// </summary>
internal sealed unsafe class SysEventsWindow : IDisposable
{
    private const string ClassName = "Clicalo.SysEvents";

    private static readonly Lock ClassGate = new();
    private static ushort _classAtom;

    [ThreadStatic]
    private static Dictionary<nint, SysEventsWindow>? _windowsOfThread;

    private readonly SysEventsThread _owner;
    private readonly Lock _handlersGate = new();
    private ImmutableDictionary<uint, ImmutableArray<SysEventsThread.MessageHandler>> _handlers =
        ImmutableDictionary<uint, ImmutableArray<SysEventsThread.MessageHandler>>.Empty;
    private HWND _handle;

    private SysEventsWindow(SysEventsThread owner) => _owner = owner;

    /// <summary>The window handle; zero once destroyed.</summary>
    public HWND Handle => _handle;

    /// <summary>
    /// Creates a message-only window (<c>HWND_MESSAGE</c>): it receives posted and sent messages, <c>WM_HOTKEY</c>
    /// and WinEvent-driven work, but never broadcasts and never the foreground.
    /// </summary>
    public static SysEventsWindow CreateMessageOnly(SysEventsThread owner) =>
        Create(owner, default, WINDOW_STYLE.WS_OVERLAPPED, HWND.HWND_MESSAGE);

    /// <summary>
    /// Creates a hidden top-level tool window, 0×0 and off screen: it receives broadcasts such as
    /// <c>TaskbarCreated</c> and can be the foreground window (the tray menu), yet it is never shown, so Windows never
    /// activates it on its own and it is absent from Alt+Tab and the taskbar.
    /// </summary>
    public static SysEventsWindow CreateHiddenTopLevel(SysEventsThread owner) =>
        Create(owner, WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, WINDOW_STYLE.WS_POPUP, default);

    /// <summary>Routes <paramref name="message"/> to <paramref name="handler"/> until the registration is disposed.</summary>
    public IDisposable AddHandler(uint message, SysEventsThread.MessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_handlersGate)
        {
            var list = _handlers.TryGetValue(message, out var existing) ? existing : [];
            _handlers = _handlers.SetItem(message, list.Add(handler));
        }

        return new Registration(this, message, handler);
    }

    /// <summary>Destroys the window. Must run on the SysEvents thread.</summary>
    public void Dispose()
    {
        var handle = _handle;
        if (handle.IsNull)
        {
            return;
        }

        _handle = default;
        _windowsOfThread?.Remove(handle);
        _ = PInvoke.DestroyWindow(handle);
    }

    /// <summary>Destroys every window still alive on the calling SysEvents thread (end of its message loop).</summary>
    public static void DestroyAllOfThread()
    {
        if (_windowsOfThread is not { } windows)
        {
            return;
        }

        foreach (var window in windows.Values.ToList())
        {
            window.Dispose();
        }
    }

    private static SysEventsWindow Create(
        SysEventsThread owner,
        WINDOW_EX_STYLE exStyle,
        WINDOW_STYLE style,
        HWND parent
    )
    {
        if (!owner.CheckAccess())
        {
            throw new InvalidOperationException(
                "SysEvents windows are created on the SysEvents thread."
            );
        }

        var instance = (HINSTANCE)(nint)PInvoke.GetModuleHandle((PCWSTR)null).Value;
        RegisterClass(instance);
        var window = new SysEventsWindow(owner);
        HWND handle;
        fixed (char* className = ClassName)
        {
            handle = PInvoke.CreateWindowEx(
                exStyle,
                className,
                null,
                style,
                parent.IsNull ? OffScreen : 0,
                parent.IsNull ? OffScreen : 0,
                0,
                0,
                parent,
                default,
                instance,
                null
            );
        }

        if (handle.IsNull)
        {
            throw new InvalidOperationException(Win32Failure("CreateWindowEx"));
        }

        window._handle = handle;
        (_windowsOfThread ??= []).Add(handle, window);
        return window;
    }

    private const int OffScreen = -32000;

    private static void RegisterClass(HINSTANCE instance)
    {
        lock (ClassGate)
        {
            if (_classAtom != 0)
            {
                return;
            }

            fixed (char* className = ClassName)
            {
                var windowClass = new WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = &WindowProcedure,
                    hInstance = instance,
                    lpszClassName = className,
                };
                _classAtom = PInvoke.RegisterClassEx(in windowClass);
            }

            if (_classAtom == 0)
            {
                throw new InvalidOperationException(Win32Failure("RegisterClassEx"));
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProcedure(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (
            _windowsOfThread is { } windows
            && windows.TryGetValue(hwnd, out var window)
            && window.Dispatch(message, wParam, lParam)
        )
        {
            return new LRESULT(0);
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static string Win32Failure(string function) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{function} failed with Win32 error {Marshal.GetLastPInvokeError()}."
        );

    private bool Dispatch(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (!Volatile.Read(ref _handlers).TryGetValue(message, out var handlers))
        {
            return false;
        }

        var handled = false;
        foreach (var handler in handlers)
        {
            try
            {
                handled |= handler((nint)wParam.Value, lParam.Value);
            }
            catch (Exception ex)
            {
                // An exception must never cross the unmanaged boundary: it would end the process without a trace.
                _owner.ReportUnhandled(ex);
                handled = true;
            }
        }

        return handled;
    }

    private void Remove(uint message, SysEventsThread.MessageHandler handler)
    {
        lock (_handlersGate)
        {
            if (!_handlers.TryGetValue(message, out var list))
            {
                return;
            }

            var remaining = list.Remove(handler);
            _handlers = remaining.IsEmpty
                ? _handlers.Remove(message)
                : _handlers.SetItem(message, remaining);
        }
    }

    private sealed class Registration(
        SysEventsWindow window,
        uint message,
        SysEventsThread.MessageHandler handler
    ) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                window.Remove(message, handler);
            }
        }
    }
}
