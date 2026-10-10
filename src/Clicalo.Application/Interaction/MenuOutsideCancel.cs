using Clicalo.Application.Ports;

namespace Clicalo.Application.Interaction;

/// <summary>
/// Cancels the menu of a shortcut of the Tab view from outside Clícalo (CUA-014): with Esc, and with a touch on
/// another app when a finger opened the menu. The composition tells it when the menu shows beside the bar
/// (<see cref="Apply"/>); it takes Esc only for that long, and asks to close the menu on the thread that owns it.
/// </summary>
/// <remarks>
/// A touch outside is seen as the pointer going to a new place outside Clícalo. A mouse or a pen do that just by moving
/// from the shortcut to the menu, so the pointer only cancels a menu that a long press of a finger opened; one opened
/// with a right click, the pen or the accessible secondary action is cancelled with Esc, [cancel] or another shortcut.
/// It lives as long as the process; the platform gives Esc back when its signals are disposed.
/// </remarks>
public sealed class MenuOutsideCancel
{
    private readonly IMenuCancelSignals _signals;
    private readonly Action<Action> _post;
    private readonly Action _close;
    private bool _open;
    private bool _pointerCancels;

    /// <summary>Starts listening; nothing is watched until a menu opens.</summary>
    /// <param name="signals">Esc and the pointer outside Clícalo.</param>
    /// <param name="post">Runs work on the thread that owns the menu.</param>
    /// <param name="close">Closes the menu.</param>
    public MenuOutsideCancel(IMenuCancelSignals signals, Action<Action> post, Action close)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(close);
        _signals = signals;
        _post = post;
        _close = close;
        _signals.EscapePressed += OnEscape;
        _signals.PointerWentOutside += OnPointerOutside;
    }

    /// <summary>The menu shows beside the bar, or no longer does. Called on the thread that owns the menu.</summary>
    /// <param name="menuOpen">Whether the menu of a shortcut shows beside the bar.</param>
    /// <param name="openedByFinger">Whether a long press of a finger opened it.</param>
    public void Apply(bool menuOpen, bool openedByFinger)
    {
        _pointerCancels = menuOpen && openedByFinger;
        if (menuOpen == _open)
        {
            return;
        }

        _open = menuOpen;
        _signals.WatchEscape(menuOpen);
    }

    private void OnEscape(object? sender, EventArgs e) =>
        _post(() =>
        {
            if (_open)
            {
                _close();
            }
        });

    private void OnPointerOutside(object? sender, EventArgs e) =>
        _post(() =>
        {
            if (_open && _pointerCancels)
            {
                _close();
            }
        });
}
