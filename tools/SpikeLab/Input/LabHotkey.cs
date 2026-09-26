using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>
/// The global shortcut of the laboratory, Ctrl+Shift+F11 (S4 row 8): <c>RegisterHotKey</c> on a message-only window of
/// the UI thread. Its <c>WM_HOTKEY</c> gives SpikeLab the right to take the foreground (origin <c>GlobalHotkey</c>).
/// The product's configurable global shortcut arrives in M3.
/// </summary>
internal sealed class LabHotkey : IDisposable
{
    private const int HotkeyId = 0x534C;
    private const int MessageOnlyParent = -3;
    private const uint F11 = 0x7A;

    private readonly Action _pressed;
    private HwndSource? _window;
    private bool _registered;

    /// <summary>Creates the shortcut; <see cref="Register"/> registers it.</summary>
    /// <param name="pressed">Runs on the UI thread when the shortcut is pressed.</param>
    public LabHotkey(Action pressed) => _pressed = pressed;

    /// <summary>The shortcut as the maintainer says it.</summary>
    public static string Description => "Ctrl+Mayús+F11";

    /// <summary>Registers the shortcut; throws when another program owns it.</summary>
    public void Register()
    {
        _window = new HwndSource(
            new HwndSourceParameters("Clicalo.SpikeLab.Hotkey") { ParentWindow = MessageOnlyParent }
        );
        _window.AddHook(OnMessage);
        _registered = PInvoke.RegisterHotKey(
            (HWND)_window.Handle,
            HotkeyId,
            HOT_KEY_MODIFIERS.MOD_CONTROL
                | HOT_KEY_MODIFIERS.MOD_SHIFT
                | HOT_KEY_MODIFIERS.MOD_NOREPEAT,
            F11
        );
        if (!_registered)
        {
            throw new InvalidOperationException(
                "RegisterHotKey("
                    + Description
                    + ") falló con el error Win32 "
                    + Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)
                    + ": otro programa ya usa ese atajo."
            );
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_window is null)
        {
            return;
        }

        if (_registered)
        {
            _ = PInvoke.UnregisterHotKey((HWND)_window.Handle, HotkeyId);
            _registered = false;
        }

        _window.RemoveHook(OnMessage);
        _window.Dispose();
        _window = null;
    }

    private nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == PInvoke.WM_HOTKEY && wParam == HotkeyId)
        {
            handled = true;
            _pressed();
        }

        return 0;
    }
}
