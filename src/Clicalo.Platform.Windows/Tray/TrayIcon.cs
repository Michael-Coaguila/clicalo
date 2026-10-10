using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The notification area icon (blueprint §8.1): <c>Shell_NotifyIcon</c> with <c>NOTIFYICON_VERSION_4</c> and its
/// callback message on the <see cref="SysEventsThread"/>. Clicking it never activates a window by itself: the click
/// only gives foreground rights (<c>LeaseOrigin.Tray</c>); showing the panel is passive and the menu goes through
/// <see cref="TrayMenuHost"/> inside a <c>TrayMenu</c> lease. It is re-added after <c>TaskbarCreated</c>. It shows the
/// icon of Clícalo, at 55 % while the panel is hidden or Clícalo is paused (BUR-003, BUR-004).
/// </summary>
/// <remarks>
/// The callback window is a hidden top-level window of the SysEvents thread, not its message-only window, because
/// <c>TaskbarCreated</c> is a broadcast and message-only windows receive no broadcasts.
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = PInvoke.WM_APP + 2;
    private const uint IconId = 1;
    private const int MaxTooltip = 127;

    private readonly TrayIconImages _images = new();
    private SysEventsWindow? _window;
    private string _tooltip = string.Empty;
    private bool _dimmed;
    private bool _added;
    private bool _wanted;
    private int _disposed;

    /// <summary>Creates the icon on <paramref name="thread"/>.</summary>
    public TrayIcon(SysEventsThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        Thread = thread;
    }

    /// <summary>Primary click or <c>NIN_SELECT</c>/<c>NIN_KEYSELECT</c>: show or hide the panel.</summary>
    public event EventHandler<TrayIconEventArgs>? Invoked;

    /// <summary>Secondary click or <c>WM_CONTEXTMENU</c>: open the menu.</summary>
    public event EventHandler<TrayIconEventArgs>? MenuRequested;

    /// <summary>The thread that owns the callback window.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>True while the icon is in the notification area.</summary>
    public bool IsShown => Volatile.Read(ref _added);

    /// <summary>Adds the icon with the localized <paramref name="tooltip"/>.</summary>
    /// <param name="tooltip">The accessible text of the icon.</param>
    /// <param name="dimmed">Whether the icon shows at 55 %: the panel is hidden or Clícalo is paused (BUR-003).</param>
    public Task ShowAsync(string tooltip, bool dimmed = false)
    {
        ArgumentNullException.ThrowIfNull(tooltip);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Thread.InvokeAsync(() =>
        {
            _tooltip = tooltip;
            _dimmed = dimmed;
            _wanted = true;
            if (_window is null)
            {
                _window = Thread.CreateHiddenWindow();
                _ = _window.AddHandler(CallbackMessage, OnCallback);
                var taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");

                AllowFromExplorer(_window.Handle, taskbarCreated);
                _ = _window.AddHandler(
                    taskbarCreated,
                    (_, _) =>
                    {
                        // Explorer (re)started: its notification area forgot every icon, or was not ready when the
                        // icon was first added (sign-in).
                        if (_wanted)
                        {
                            Volatile.Write(ref _added, false);
                            Add();
                        }

                        return false;
                    }
                );
            }

            if (_added)
            {
                Modify();
            }
            else
            {
                Add();
            }
        });
    }

    /// <summary>Changes the localized tooltip (language change).</summary>
    public Task SetTooltipAsync(string tooltip)
    {
        ArgumentNullException.ThrowIfNull(tooltip);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Thread.InvokeAsync(() =>
        {
            _tooltip = tooltip;
            if (_added)
            {
                Modify();
            }
        });
    }

    /// <summary>
    /// Changes the text and the look of the icon together: the hidden panel and the pause are said in the text and
    /// shown at 55 % (BUR-003, BUR-004).
    /// </summary>
    /// <param name="tooltip">The accessible text of the icon.</param>
    /// <param name="dimmed">Whether the icon shows at 55 %.</param>
    public Task SetAppearanceAsync(string tooltip, bool dimmed)
    {
        ArgumentNullException.ThrowIfNull(tooltip);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Thread.InvokeAsync(() =>
        {
            _tooltip = tooltip;
            _dimmed = dimmed;
            if (_added)
            {
                Modify();
            }
        });
    }

    /// <summary>Removes the icon.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            Thread.Post(() =>
            {
                _wanted = false;
                if (_added)
                {
                    var data = Data();
                    _ = ShellNotifyIcon.Send(NOTIFY_ICON_MESSAGE.NIM_DELETE, ref data);
                    _added = false;
                }

                _window?.Dispose();
                _window = null;
                _images.Dispose();
            });
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop has ended; the shell removes the icon of a destroyed window by itself.
            _images.Dispose();
        }
    }

    /// <summary>
    /// Lets <paramref name="message"/> through UIPI: an elevated Clícalo (EJE-013) would otherwise never hear the
    /// <c>TaskbarCreated</c> broadcast of the medium-integrity Explorer and lose its icon when Explorer restarts.
    /// </summary>
    private static unsafe void AllowFromExplorer(HWND window, uint message) =>
        _ = PInvoke.ChangeWindowMessageFilterEx(
            window,
            message,
            WINDOW_MESSAGE_FILTER_ACTION.MSGFLT_ALLOW,
            null
        );

    private static PhysicalPoint Anchor(nint wParam) =>
        new((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));

    private unsafe void Add()
    {
        var data = Data();
        data.Flags = (uint)(
            NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE
            | NOTIFY_ICON_DATA_FLAGS.NIF_ICON
            | NOTIFY_ICON_DATA_FLAGS.NIF_TIP
            | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP
        );
        data.CallbackMessage = CallbackMessage;
        data.Icon = _images.Handle(_dimmed);
        CopyTooltip(ref data);
        if (!ShellNotifyIcon.Send(NOTIFY_ICON_MESSAGE.NIM_ADD, ref data))
        {
            throw new InvalidOperationException("Shell_NotifyIcon(NIM_ADD) failed.");
        }

        data.TimeoutOrVersion = PInvoke.NOTIFYICON_VERSION_4;
        _ = ShellNotifyIcon.Send(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, ref data);
        Volatile.Write(ref _added, true);
    }

    private void Modify()
    {
        var data = Data();
        data.Flags = (uint)(
            NOTIFY_ICON_DATA_FLAGS.NIF_ICON
            | NOTIFY_ICON_DATA_FLAGS.NIF_TIP
            | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP
        );
        data.Icon = _images.Handle(_dimmed);
        CopyTooltip(ref data);
        _ = ShellNotifyIcon.Send(NOTIFY_ICON_MESSAGE.NIM_MODIFY, ref data);
    }

    private NotifyIconData Data() =>
        new() { Window = (nint)(_window?.Handle ?? default(HWND)), Id = IconId };

    private unsafe void CopyTooltip(ref NotifyIconData data)
    {
        var text = _tooltip.Length > MaxTooltip ? _tooltip[..MaxTooltip] : _tooltip;
        fixed (char* tip = data.Tip)
        {
            text.AsSpan().CopyTo(new Span<char>(tip, MaxTooltip));
            tip[text.Length] = '\0';
        }
    }

    private bool OnCallback(nint wParam, nint lParam)
    {
        // NOTIFYICON_VERSION_4: LOWORD(lParam) is the event, HIWORD(lParam) the icon id, wParam the anchor point.
        var notification = (uint)(lParam & 0xFFFF);
        switch (notification)
        {
            case PInvoke.NIN_SELECT or PInvoke.NIN_SELECT | PInvoke.NINF_KEY:
                Invoked?.Invoke(this, new TrayIconEventArgs(Anchor(wParam)));
                return true;

            case PInvoke.WM_CONTEXTMENU:
                MenuRequested?.Invoke(this, new TrayIconEventArgs(Anchor(wParam)));
                return true;

            default:
                return false;
        }
    }
}
