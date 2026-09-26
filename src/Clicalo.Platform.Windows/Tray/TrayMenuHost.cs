using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The tray menu (blueprint §3.6, §8.1): an own hidden top-level window (<c>WS_EX_TOOLWINDOW</c>, 0×0 off screen) on
/// the <see cref="SysEventsThread"/>, the ONLY file allowed to call <c>TrackPopupMenuEx</c>
/// (banned-api-exceptions.json: tray-menu-host). It runs inside a <c>TrayMenu</c> lease whose target is
/// <see cref="Window"/>; after the menu closes it posts <c>WM_NULL</c> and the lease restores the previous window.
/// </summary>
/// <remarks>
/// The host window is never shown: Windows never activates a hidden window on its own (for example when the
/// foreground app closes), yet <c>SetForegroundWindow</c> under the lease makes it the foreground window, which is what
/// <c>TrackPopupMenuEx</c> needs to close the menu when the user taps elsewhere.
/// </remarks>
public sealed class TrayMenuHost : IDisposable
{
    // TrackPopupMenuEx flags: the command is returned instead of posted, both buttons select, and the menu opens up
    // and to the left of the anchor (the notification area is at the screen edge). Without TPM_NONOTIFY, so that
    // WM_ENTERMENULOOP reports the open menu.
    private const uint TpmRightButton = 0x0002;
    private const uint TpmRightAlign = 0x0008;
    private const uint TpmBottomAlign = 0x0020;
    private const uint TpmReturnCommand = 0x0100;

    private SysEventsWindow? _window;
    private HWND _handle;
    private int _menuOpen;
    private int _disposed;

    /// <summary>Creates the host on <paramref name="thread"/>.</summary>
    public TrayMenuHost(SysEventsThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        Thread = thread;
    }

    /// <summary>Raised on the SysEvents thread when the menu's modal loop starts (the menu is on screen).</summary>
    public event EventHandler? MenuOpened;

    /// <summary>The thread that owns the host window.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>The hidden host window: the target of the <c>TrayMenu</c> lease.</summary>
    public WindowToken Window => new(_handle);

    /// <summary>True while <see cref="ShowMenuAsync"/> is showing a menu.</summary>
    public bool IsMenuOpen => Volatile.Read(ref _menuOpen) != 0;

    /// <summary>Creates the hidden host window on the SysEvents thread.</summary>
    public Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Thread.InvokeAsync(() =>
        {
            if (_window is not null)
            {
                return;
            }

            _window = Thread.CreateHiddenWindow();
            _ = _window.AddHandler(
                PInvoke.WM_ENTERMENULOOP,
                (_, _) =>
                {
                    MenuOpened?.Invoke(this, EventArgs.Empty);
                    return false;
                }
            );
            _handle = _window.Handle;
        });
    }

    /// <summary>
    /// Shows <paramref name="items"/> at <paramref name="anchor"/> with <c>TrackPopupMenuEx(TPM_RETURNCMD)</c> while
    /// the host is in the foreground. Completes with the chosen <see cref="TrayMenuItem.Id"/>, or null when the menu
    /// was dismissed.
    /// </summary>
    public Task<int?> ShowMenuAsync(IReadOnlyList<TrayMenuItem> items, PhysicalPoint anchor)
    {
        ArgumentNullException.ThrowIfNull(items);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_handle.IsNull)
        {
            throw new InvalidOperationException("Call StartAsync before showing the menu.");
        }

        return Thread.InvokeAsync(() => ShowMenu(items, anchor));
    }

    /// <summary>
    /// Closes the open menu as if the user dismissed it (<c>EndMenu</c> on the SysEvents thread): used when the app
    /// exits or releases everything with the menu open, and by the desktop tests. No effect without a menu.
    /// </summary>
    public void DismissMenu() =>
        Thread.Post(() =>
        {
            if (IsMenuOpen)
            {
                _ = PInvoke.EndMenu();
            }
        });

    /// <summary>Destroys the host window.</summary>
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
                _window?.Dispose();
                _window = null;
                _handle = default;
            });
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop has ended and destroyed its windows.
        }
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The tray menu opens only here, inside the TrayMenu lease, with verified restoration afterwards (ADR-0005)."
    )]
    private unsafe int? ShowMenu(IReadOnlyList<TrayMenuItem> items, PhysicalPoint anchor)
    {
        var menu = PInvoke.CreatePopupMenu();
        if (menu.IsNull)
        {
            throw new InvalidOperationException("CreatePopupMenu failed.");
        }

        try
        {
            foreach (var item in items)
            {
                if (item.Id <= 0)
                {
                    throw new ArgumentException("Menu item ids are positive.", nameof(items));
                }

                var flags = item.IsEnabled
                    ? MENU_ITEM_FLAGS.MF_STRING
                    : MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_GRAYED;
                fixed (char* text = item.Text)
                {
                    _ = PInvoke.AppendMenu(menu, flags, (nuint)item.Id, text);
                }
            }

            Volatile.Write(ref _menuOpen, 1);
            var chosen = PInvoke.TrackPopupMenuEx(
                menu,
                TpmReturnCommand | TpmRightButton | TpmRightAlign | TpmBottomAlign,
                anchor.X,
                anchor.Y,
                _handle,
                null
            );

            // KB135788: without this message the menu may not close the next time the user taps elsewhere.
            _ = PInvoke.PostMessage(_handle, PInvoke.WM_NULL, default, default);
            return chosen.Value > 0 ? chosen.Value : null;
        }
        finally
        {
            Volatile.Write(ref _menuOpen, 0);
            _ = PInvoke.DestroyMenu(menu);
        }
    }
}
