using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Platform.Windows.Hotkeys;

/// <summary>
/// The optional global shortcut that shows or hides the panel (BUR-005, user decision D10): <c>RegisterHotKey</c> with
/// <c>MOD_NOREPEAT</c> on the message window of the <see cref="SysEventsThread"/>. It is off until a combination is
/// applied, and at most one combination is registered at a time.
/// </summary>
/// <remarks>
/// Windows consumes a registered hotkey, so its last key never reaches the app in front, and <c>WM_HOTKEY</c> is
/// posted to Clícalo's thread without activating any window: <see cref="Pressed"/> only asks for the panel to be shown
/// or hidden, which is passive (REG-01). When another program already owns the combination the registration fails
/// and <see cref="ApplyAsync"/> says so; nothing is registered then.
/// </remarks>
public sealed class GlobalPanelHotkey : IDisposable
{
    /// <summary>The <c>RegisterHotKey</c> id of the panel shortcut (the foreground rights hotkey uses 0x4C43).</summary>
    public const int HotkeyId = 0x4C44;

    private readonly SysEventsThread _thread;
    private IDisposable? _handler;
    private HotkeyChord? _registered;
    private int _disposed;

    /// <summary>Creates the shortcut on <paramref name="thread"/>; nothing is registered yet.</summary>
    /// <param name="thread">The thread whose message window owns the registration.</param>
    public GlobalPanelHotkey(SysEventsThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        _thread = thread;
    }

    /// <summary>The combination was pressed; raised on the SysEvents thread.</summary>
    public event EventHandler? Pressed;

    /// <summary>The combination registered now, or <see langword="null"/> while the shortcut is off.</summary>
    public HotkeyChord? Registered => _registered;

    /// <summary>
    /// Registers <paramref name="chord"/> in place of the one in use, or only unregisters with
    /// <see langword="null"/>. Completes with <see langword="false"/> when Windows refuses the combination (another
    /// program owns it): the shortcut is then off.
    /// </summary>
    /// <param name="chord">The combination, or <see langword="null"/> to turn the shortcut off.</param>
    public Task<bool> ApplyAsync(HotkeyChord? chord)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _thread.InvokeAsync(() =>
        {
            if (_registered == chord)
            {
                return true;
            }

            Unregister();
            if (chord is not { } wanted)
            {
                return true;
            }

            _handler ??= _thread.AddMessageHandler(PInvoke.WM_HOTKEY, OnHotkey);
            var registered = PInvoke.RegisterHotKey(
                (HWND)_thread.MessageWindow,
                HotkeyId,
                (HOT_KEY_MODIFIERS)wanted.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT,
                wanted.VirtualKey
            );
            if (registered)
            {
                _registered = wanted;
            }

            return (bool)registered;
        });
    }

    /// <summary>Unregisters the combination.</summary>
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
            _registered = null;
        }
    }

    private void Unregister()
    {
        if (_registered is null)
        {
            return;
        }

        _ = PInvoke.UnregisterHotKey((HWND)_thread.MessageWindow, HotkeyId);
        _registered = null;
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
