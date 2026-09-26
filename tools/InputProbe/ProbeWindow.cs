using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Tools.InputProbe;

/// <summary>
/// The instrumented top-level window. It behaves like an ordinary application window (it translates keys into
/// characters and lets <c>DefWindowProc</c> do its work) and records every input, focus and activation message.
/// </summary>
/// <remarks>
/// Deliberate deviations from <c>DefWindowProc</c>, so that recorded input never changes the probe's own state:
/// <c>SC_KEYMENU</c> is swallowed (Alt or F10 alone would enter the invisible system-menu loop and eat the next
/// keys) and <c>WM_SYSCHAR</c> is not forwarded (it would beep for a missing mnemonic).
/// Raw Input is registered without <c>RIDEV_INPUTSINK</c>: the probe only sees keys while it is in the
/// foreground and can never act as a keylogger for other applications.
/// </remarks>
internal sealed unsafe class ProbeWindow
{
    private const string ClassName = "Clicalo.InputProbe";
    private const uint CommandMessage = PInvoke.WM_APP + 1;
    private const ushort GenericDesktopPage = 0x01;
    private const ushort KeyboardUsage = 0x06;
    private const int InitialWidth = 640;
    private const int InitialHeight = 400;

    // The window procedure is a static unmanaged callback; the probe creates exactly one window per process.
    private static ProbeWindow? _current;

    private readonly EventEmitter _events;
    private readonly ConcurrentQueue<ProbeCommand> _commands = new();
    private HWND _hwnd;
    private char _pendingHighSurrogate;

    public ProbeWindow(EventEmitter events)
    {
        _events = events;
    }

    /// <summary>Registers the class, creates the (hidden) window and registers for keyboard Raw Input.</summary>
    public bool TryCreate([NotNullWhen(false)] out string? failure)
    {
        _current = this;
        var instance = (HINSTANCE)(nint)PInvoke.GetModuleHandle((PCWSTR)null).Value;
        var title = string.Create(
            CultureInfo.InvariantCulture,
            $"InputProbe {Environment.ProcessId}"
        );
        fixed (char* className = ClassName)
        fixed (char* windowName = title)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                style =
                    WNDCLASS_STYLES.CS_DBLCLKS
                    | WNDCLASS_STYLES.CS_HREDRAW
                    | WNDCLASS_STYLES.CS_VREDRAW,
                lpfnWndProc = &WindowProcedure,
                hInstance = instance,
                hCursor = PInvoke.LoadCursor(default, PInvoke.IDC_ARROW),
                hbrBackground = PInvoke.GetSysColorBrush(SYS_COLOR_INDEX.COLOR_WINDOW),
                lpszClassName = className,
            };
            if (PInvoke.RegisterClassEx(in windowClass) == 0)
            {
                failure = Win32Failure("RegisterClassEx");
                return false;
            }

            _hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_LEFT,
                className,
                windowName,
                WINDOW_STYLE.WS_OVERLAPPEDWINDOW,
                PInvoke.CW_USEDEFAULT,
                PInvoke.CW_USEDEFAULT,
                InitialWidth,
                InitialHeight,
                default,
                default,
                instance,
                null
            );
        }

        if (_hwnd.IsNull)
        {
            failure = Win32Failure("CreateWindowEx");
            return false;
        }

        var keyboard = new RAWINPUTDEVICE
        {
            usUsagePage = GenericDesktopPage,
            usUsage = KeyboardUsage,
            // No flags on purpose: without RIDEV_INPUTSINK the probe only receives Raw Input while it is in the
            // foreground, and without RIDEV_NOLEGACY the legacy WM_KEY* messages keep flowing.
            hwndTarget = _hwnd,
        };
        if (!PInvoke.RegisterRawInputDevices(&keyboard, 1, (uint)sizeof(RAWINPUTDEVICE)))
        {
            failure = Win32Failure("RegisterRawInputDevices");
            PInvoke.DestroyWindow(_hwnd);
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>
    /// Shows the window, asks for the foreground (granted only if the client allowed it) and emits the ready
    /// event with everything a client needs to target and interpret the probe.
    /// </summary>
    public void ShowAndAnnounceReady()
    {
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNORMAL);
        _ = PInvoke.SetForegroundWindow(_hwnd);

        var json = _events.Begin(ProbeEventKinds.Ready);
        json.WriteNumber(ProbeFields.Protocol, ProbeProtocol.Version);
        json.WriteNumber(ProbeFields.Window, (long)_hwnd.Value);
        json.WriteNumber(ProbeFields.ProcessId, Environment.ProcessId);
        json.WriteNumber(ProbeFields.ThreadId, PInvoke.GetCurrentThreadId());
        json.WriteNumber(ProbeFields.Session, Process.GetCurrentProcess().SessionId);
        json.WriteNumber(ProbeFields.TimestampFrequency, Stopwatch.Frequency);
        json.WriteNumber(ProbeFields.KeyboardLayout, (long)PInvoke.GetKeyboardLayout(0).Value);
        _events.Commit();
    }

    /// <summary>Runs the message loop until the window is destroyed; returns the process exit code.</summary>
    public static int RunMessageLoop()
    {
        MSG message;
        while (true)
        {
            var result = PInvoke.GetMessage(&message, default, 0, 0).Value;
            if (result == 0)
            {
                return (int)message.wParam.Value;
            }

            if (result == -1)
            {
                return ProbeProtocol.ExitWindow;
            }

            PInvoke.TranslateMessage(&message);
            PInvoke.DispatchMessage(&message);
        }
    }

    /// <summary>Pipe callback (thread pool): queues the command and wakes the window thread.</summary>
    public void OnCommandLine(string line)
    {
        _commands.Enqueue(ProbeCommand.Parse(line));
        PInvoke.PostMessage(_hwnd, CommandMessage, default, default);
    }

    /// <summary>Pipe callback (thread pool): the client is gone, so the probe closes.</summary>
    public void OnPipeClosed() => PInvoke.PostMessage(_hwnd, PInvoke.WM_CLOSE, default, default);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProcedure(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        var window = _current;
        if (window is null)
        {
            return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
        }

        try
        {
            return window.Handle(hwnd, message, wParam, lParam);
        }
        catch (Exception ex)
        {
            // An exception must never cross the unmanaged boundary (it would kill the process without a trace):
            // report it as an error event and let the default procedure handle the message.
            window._events.Error(
                MessageNames.Of(message) + ": " + ex.GetType().Name + ": " + ex.Message
            );
            return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private static string Win32Failure(string function) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{function} failed with Win32 error {Marshal.GetLastPInvokeError()}."
        );

    private LRESULT Handle(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
            case PInvoke.WM_KEYDOWN
            or PInvoke.WM_KEYUP
            or PInvoke.WM_SYSKEYDOWN
            or PInvoke.WM_SYSKEYUP:
                OnKey(message, wParam, lParam);
                break;

            case PInvoke.WM_CHAR
            or PInvoke.WM_DEADCHAR
            or PInvoke.WM_SYSDEADCHAR:
                OnChar(message, wParam, lParam);
                break;

            case PInvoke.WM_SYSCHAR:
                OnChar(message, wParam, lParam);
                return new LRESULT(0);

            case PInvoke.WM_UNICHAR:
                if (wParam.Value == PInvoke.UNICODE_NOCHAR)
                {
                    return new LRESULT(1);
                }

                OnUniChar(message, wParam, lParam);
                break;

            case PInvoke.WM_INPUT:
                OnRawInput(wParam, lParam);
                break;

            case PInvoke.WM_LBUTTONDOWN
            or PInvoke.WM_LBUTTONUP
            or PInvoke.WM_LBUTTONDBLCLK
            or PInvoke.WM_RBUTTONDOWN
            or PInvoke.WM_RBUTTONUP
            or PInvoke.WM_RBUTTONDBLCLK
            or PInvoke.WM_MBUTTONDOWN
            or PInvoke.WM_MBUTTONUP
            or PInvoke.WM_MBUTTONDBLCLK:
                OnMouseButton(message, wParam, lParam);
                break;

            case PInvoke.WM_XBUTTONDOWN
            or PInvoke.WM_XBUTTONUP
            or PInvoke.WM_XBUTTONDBLCLK:
                OnMouseButton(message, wParam, lParam);
                return new LRESULT(1);

            case PInvoke.WM_MOUSEWHEEL
            or PInvoke.WM_MOUSEHWHEEL:
                OnWheel(message, wParam, lParam);
                return new LRESULT(0);

            case PInvoke.WM_ACTIVATE:
                OnActivate(message, wParam, lParam);
                break;

            case PInvoke.WM_ACTIVATEAPP:
                OnActivateApp(message, wParam, lParam);
                break;

            case PInvoke.WM_SETFOCUS
            or PInvoke.WM_KILLFOCUS:
                OnFocus(message, wParam, lParam);
                break;

            case PInvoke.WM_INPUTLANGCHANGE:
                OnInputLanguage(message, wParam, lParam);
                break;

            case PInvoke.WM_NCACTIVATE
            or PInvoke.WM_MOUSEACTIVATE
            or PInvoke.WM_IME_SETCONTEXT
            or PInvoke.WM_IME_NOTIFY
            or PInvoke.WM_IME_STARTCOMPOSITION
            or PInvoke.WM_IME_COMPOSITION
            or PInvoke.WM_IME_ENDCOMPOSITION
            or PInvoke.WM_IME_CHAR:
                OnOtherMessage(message, wParam, lParam);
                break;

            case PInvoke.WM_SYSCOMMAND:
                OnOtherMessage(message, wParam, lParam);
                if ((wParam.Value & 0xFFF0) == PInvoke.SC_KEYMENU)
                {
                    return new LRESULT(0);
                }

                break;

            case CommandMessage:
                RunPendingCommands();
                return new LRESULT(0);

            case PInvoke.WM_CLOSE:
                PInvoke.DestroyWindow(hwnd);
                return new LRESULT(0);

            case PInvoke.WM_DESTROY:
                _events.Begin(ProbeEventKinds.Exit);
                _events.Commit();
                PInvoke.PostQuitMessage(ProbeProtocol.ExitOk);
                return new LRESULT(0);
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private Utf8JsonWriter BeginMessage(string kind, uint message, WPARAM wParam, LPARAM lParam) =>
        BeginMessage(kind, message, wParam, lParam, PInvoke.GetMessageExtraInfo().Value);

    private Utf8JsonWriter BeginMessage(
        string kind,
        uint message,
        WPARAM wParam,
        LPARAM lParam,
        long extraInfo
    )
    {
        var json = _events.Begin(kind);
        json.WriteNumber(ProbeFields.Message, message);
        json.WriteString(ProbeFields.MessageName, MessageNames.Of(message));
        json.WriteNumber(ProbeFields.MessageTime, PInvoke.GetMessageTime());
        json.WriteNumber(ProbeFields.WParam, (ulong)wParam.Value);
        json.WriteNumber(ProbeFields.LParam, (long)lParam.Value);
        json.WriteNumber(ProbeFields.ExtraInfo, extraInfo);
        return json;
    }

    private void OnKey(uint message, WPARAM wParam, LPARAM lParam)
    {
        var virtualKey = (ushort)wParam.Value;
        var flags = KeystrokeFlags.FromLParam(lParam.Value);
        var json = BeginMessage(ProbeEventKinds.Key, message, wParam, lParam);
        json.WriteNumber(ProbeFields.VirtualKey, virtualKey);
        json.WriteNumber(ProbeFields.SideVirtualKey, KeyboardState.SideSpecific(virtualKey, flags));
        WriteKeystrokeFlags(json, flags);
        json.WriteNumber(ProbeFields.Modifiers, KeyboardState.SideModifiers());
        _events.Commit();
    }

    private void OnChar(uint message, WPARAM wParam, LPARAM lParam)
    {
        var unit = (char)(ushort)wParam.Value;
        var text =
            message == PInvoke.WM_CHAR
                ? AssembleText(unit)
                : (char.IsSurrogate(unit) ? null : new string(unit, 1));
        var json = BeginMessage(ProbeEventKinds.Char, message, wParam, lParam);
        json.WriteNumber(ProbeFields.CodeUnit, unit);
        if (text is not null)
        {
            json.WriteString(ProbeFields.Text, text);
        }

        WriteKeystrokeFlags(json, KeystrokeFlags.FromLParam(lParam.Value));
        json.WriteNumber(ProbeFields.Modifiers, KeyboardState.SideModifiers());
        _events.Commit();
    }

    /// <summary>
    /// Joins the two <c>WM_CHAR</c> halves of a supplementary character (an emoji arrives as a high surrogate
    /// followed by a low surrogate) so that each complete character is reported once, on its last unit.
    /// </summary>
    private string? AssembleText(char unit)
    {
        if (char.IsHighSurrogate(unit))
        {
            _pendingHighSurrogate = unit;
            return null;
        }

        var pending = _pendingHighSurrogate;
        _pendingHighSurrogate = '\0';
        if (char.IsLowSurrogate(unit))
        {
            return pending == '\0' ? null : new string([pending, unit]);
        }

        return new string(unit, 1);
    }

    private void OnUniChar(uint message, WPARAM wParam, LPARAM lParam)
    {
        var codePoint = (uint)wParam.Value;
        var json = BeginMessage(ProbeEventKinds.UniChar, message, wParam, lParam);
        json.WriteNumber(ProbeFields.CodePoint, codePoint);
        if (Rune.IsValid(codePoint))
        {
            json.WriteString(ProbeFields.Text, new Rune(codePoint).ToString());
        }

        _events.Commit();
    }

    private void OnRawInput(WPARAM wParam, LPARAM lParam)
    {
        RAWINPUT input;
        var size = (uint)sizeof(RAWINPUT);
        var copied = PInvoke.GetRawInputData(
            new HRAWINPUT(lParam.Value),
            RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
            &input,
            &size,
            (uint)sizeof(RAWINPUTHEADER)
        );
        if (copied == uint.MaxValue)
        {
            _events.Error(Win32Failure("GetRawInputData"));
            return;
        }

        if ((RID_DEVICE_INFO_TYPE)input.header.dwType != RID_DEVICE_INFO_TYPE.RIM_TYPEKEYBOARD)
        {
            return;
        }

        var keyboard = input.data.keyboard;
        // For Raw Input the extra information travels in RAWKEYBOARD, not in GetMessageExtraInfo.
        var json = BeginMessage(
            ProbeEventKinds.RawKey,
            PInvoke.WM_INPUT,
            wParam,
            lParam,
            keyboard.ExtraInformation
        );
        json.WriteNumber(ProbeFields.ScanCode, keyboard.MakeCode);
        json.WriteNumber(ProbeFields.RawFlags, keyboard.Flags);
        json.WriteNumber(ProbeFields.VirtualKey, keyboard.VKey);
        json.WriteNumber(ProbeFields.RawMessage, keyboard.Message);
        json.WriteNumber(ProbeFields.Device, (long)input.header.hDevice.Value);
        json.WriteBoolean(ProbeFields.Sink, (wParam.Value & 0xFF) != 0);
        _events.Commit();
    }

    private void OnMouseButton(uint message, WPARAM wParam, LPARAM lParam)
    {
        var (button, down, doubleClick) = message switch
        {
            PInvoke.WM_LBUTTONDOWN => ("left", true, false),
            PInvoke.WM_LBUTTONUP => ("left", false, false),
            PInvoke.WM_LBUTTONDBLCLK => ("left", true, true),
            PInvoke.WM_RBUTTONDOWN => ("right", true, false),
            PInvoke.WM_RBUTTONUP => ("right", false, false),
            PInvoke.WM_RBUTTONDBLCLK => ("right", true, true),
            PInvoke.WM_MBUTTONDOWN => ("middle", true, false),
            PInvoke.WM_MBUTTONUP => ("middle", false, false),
            PInvoke.WM_MBUTTONDBLCLK => ("middle", true, true),
            _ => (
                HighWord(wParam.Value) == PInvoke.XBUTTON2 ? "x2" : "x1",
                message != PInvoke.WM_XBUTTONUP,
                message == PInvoke.WM_XBUTTONDBLCLK
            ),
        };
        var json = BeginMessage(ProbeEventKinds.MouseButton, message, wParam, lParam);
        json.WriteString(ProbeFields.Button, button);
        json.WriteBoolean(ProbeFields.Down, down);
        json.WriteBoolean(ProbeFields.DoubleClick, doubleClick);
        WritePoint(json, lParam);
        json.WriteNumber(ProbeFields.KeyFlags, LowWord(wParam.Value));
        _events.Commit();
    }

    private void OnWheel(uint message, WPARAM wParam, LPARAM lParam)
    {
        var json = BeginMessage(ProbeEventKinds.Wheel, message, wParam, lParam);
        json.WriteBoolean(ProbeFields.Horizontal, message == PInvoke.WM_MOUSEHWHEEL);
        json.WriteNumber(ProbeFields.Delta, (short)HighWord(wParam.Value));
        WritePoint(json, lParam);
        json.WriteNumber(ProbeFields.KeyFlags, LowWord(wParam.Value));
        _events.Commit();
    }

    private void OnActivate(uint message, WPARAM wParam, LPARAM lParam)
    {
        var json = BeginMessage(ProbeEventKinds.Activate, message, wParam, lParam);
        json.WriteNumber(ProbeFields.State, LowWord(wParam.Value));
        json.WriteBoolean(ProbeFields.Minimized, HighWord(wParam.Value) != 0);
        json.WriteNumber(ProbeFields.OtherWindow, (long)lParam.Value);
        _events.Commit();
    }

    private void OnActivateApp(uint message, WPARAM wParam, LPARAM lParam)
    {
        var json = BeginMessage(ProbeEventKinds.ActivateApp, message, wParam, lParam);
        json.WriteBoolean(ProbeFields.Active, wParam.Value != 0);
        json.WriteNumber(ProbeFields.OtherThread, unchecked((uint)lParam.Value));
        _events.Commit();
    }

    private void OnFocus(uint message, WPARAM wParam, LPARAM lParam)
    {
        var json = BeginMessage(ProbeEventKinds.Focus, message, wParam, lParam);
        json.WriteBoolean(ProbeFields.Gained, message == PInvoke.WM_SETFOCUS);
        json.WriteNumber(ProbeFields.OtherWindow, (long)wParam.Value);
        _events.Commit();
    }

    private void OnInputLanguage(uint message, WPARAM wParam, LPARAM lParam)
    {
        var json = BeginMessage(ProbeEventKinds.InputLanguage, message, wParam, lParam);
        json.WriteNumber(ProbeFields.CharSet, (ulong)wParam.Value);
        json.WriteNumber(ProbeFields.KeyboardLayout, (long)lParam.Value);
        _events.Commit();
    }

    private void OnOtherMessage(uint message, WPARAM wParam, LPARAM lParam)
    {
        BeginMessage(ProbeEventKinds.Message, message, wParam, lParam);
        _events.Commit();
    }

    private void RunPendingCommands()
    {
        while (_commands.TryDequeue(out var command))
        {
            if (command.Error is not null)
            {
                _events.Error(command.Error);
                continue;
            }

            switch (command.Name)
            {
                case ProbeCommands.Ping:
                    var pong = _events.Begin(ProbeEventKinds.Pong);
                    WriteId(pong, command.Id);
                    _events.Commit();
                    break;

                case ProbeCommands.Foreground:
                    BringToForeground(command);
                    break;

                case ProbeCommands.Quit:
                    PInvoke.DestroyWindow(_hwnd);
                    return;

                default:
                    _events.Error("Unknown command '" + command.Name + "'.");
                    break;
            }
        }
    }

    private void BringToForeground(ProbeCommand command)
    {
        var target = command.Window is { } requested ? new HWND(requested) : _hwnd;
        var succeeded = PInvoke.IsWindow(target) && PInvoke.SetForegroundWindow(target);
        var json = _events.Begin(ProbeEventKinds.Foreground);
        WriteId(json, command.Id);
        json.WriteNumber(ProbeFields.Window, (long)target.Value);
        json.WriteBoolean(ProbeFields.Succeeded, succeeded);
        _events.Commit();
    }

    private static void WriteId(Utf8JsonWriter json, long? id)
    {
        if (id is { } value)
        {
            json.WriteNumber(ProbeFields.Id, value);
        }
    }

    private static void WriteKeystrokeFlags(Utf8JsonWriter json, KeystrokeFlags flags)
    {
        json.WriteNumber(ProbeFields.ScanCode, flags.ScanCode);
        json.WriteBoolean(ProbeFields.Extended, flags.IsExtended);
        json.WriteNumber(ProbeFields.RepeatCount, flags.RepeatCount);
        json.WriteBoolean(ProbeFields.AltDown, flags.IsAltDown);
        json.WriteBoolean(ProbeFields.WasDown, flags.WasDown);
        json.WriteBoolean(ProbeFields.Released, flags.IsReleased);
    }

    private static void WritePoint(Utf8JsonWriter json, LPARAM lParam)
    {
        json.WriteNumber(ProbeFields.X, (short)LowWord(unchecked((nuint)lParam.Value)));
        json.WriteNumber(ProbeFields.Y, (short)HighWord(unchecked((nuint)lParam.Value)));
    }

    private static ushort LowWord(nuint value) => (ushort)(value & 0xFFFF);

    private static ushort HighWord(nuint value) => (ushort)((value >> 16) & 0xFFFF);
}
