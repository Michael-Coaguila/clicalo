using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Platform.Windows.Hotkeys;

/// <summary>
/// Esc while the menu of a shortcut is open beside the bar of the Tab view (CUA-014): <c>RegisterHotKey</c> for the
/// bare Esc key on the message window of the <see cref="SysEventsThread"/>, held only while <see cref="Watch"/> is on.
/// It is a hotkey, not a keyboard hook: Clícalo sees that one key and no other, and only for as long as the menu shows.
/// </summary>
/// <remarks>
/// Windows consumes a registered hotkey, so while the menu is open Esc closes it and does not reach the app in front,
/// as with any menu. <c>WM_HOTKEY</c> is posted to the thread of Clícalo without activating any window (REG-01). When
/// another program already owns bare Esc the registration fails and the menu simply keeps its other ways to close.
/// </remarks>
public sealed class MenuEscapeHotkey : IDisposable
{
    /// <summary>The <c>RegisterHotKey</c> id of Esc (the panel shortcut uses 0x4C44).</summary>
    public const int HotkeyId = 0x4C45;

    private const uint VkEscape = 0x1B;

    private readonly SysEventsThread _thread;
    private IDisposable? _handler;
    private bool _registered;
    private int _disposed;

    /// <summary>Creates the hotkey on <paramref name="thread"/>; nothing is registered yet.</summary>
    /// <param name="thread">The thread whose message window owns the registration.</param>
    public MenuEscapeHotkey(SysEventsThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        _thread = thread;
    }

    /// <summary>Esc was pressed while watched; raised on the SysEvents thread.</summary>
    public event EventHandler? Pressed;

    /// <summary>Registers Esc, or gives it back. Safe from any thread; it never blocks.</summary>
    /// <param name="watch">Whether to take Esc.</param>
    public void Watch(bool watch)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _thread.Post(() =>
            {
                if (watch)
                {
                    Register();
                }
                else
                {
                    Unregister();
                }
            });
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop has ended: its window, and with it the registration, is gone.
        }
    }

    /// <summary>Gives Esc back.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _thread.Post(() =>
            {
                Unregister();
                _handler?.Dispose();
                _handler = null;
            });
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop has ended: its window, and with it the registration, is gone.
        }
    }

    private void Register()
    {
        if (_registered || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _handler ??= _thread.AddMessageHandler(PInvoke.WM_HOTKEY, OnHotkey);
        _registered = PInvoke.RegisterHotKey(
            (HWND)_thread.MessageWindow,
            HotkeyId,
            HOT_KEY_MODIFIERS.MOD_NOREPEAT,
            VkEscape
        );
    }

    private void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        _ = PInvoke.UnregisterHotKey((HWND)_thread.MessageWindow, HotkeyId);
        _registered = false;
    }

    private bool OnHotkey(nint wParam, nint lParam)
    {
        if (wParam != HotkeyId)
        {
            return false;
        }

        Pressed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
