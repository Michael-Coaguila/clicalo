namespace Clicalo.Application.Ports;

/// <summary>
/// What cancels the menu of a shortcut from outside Clícalo while it shows beside the bar of the Tab view (CUA-014):
/// the Esc key and the pointer acting on another app. Those windows never take the foreground (REG-01), so they receive
/// neither; the platform watches both without a keyboard or mouse hook and without activating anything.
/// </summary>
public interface IMenuCancelSignals
{
    /// <summary>Esc was pressed while <see cref="WatchEscape"/> was on. Raised on a platform thread.</summary>
    event EventHandler? EscapePressed;

    /// <summary>
    /// The pointer went to a new place outside the windows of Clícalo (a finger touched another app). Raised on a
    /// platform thread, whether or not Esc is watched.
    /// </summary>
    event EventHandler? PointerWentOutside;

    /// <summary>
    /// Starts or stops taking Esc. While on, Esc reaches Clícalo instead of the app in front, as with any open menu;
    /// it must be off whenever no menu is open.
    /// </summary>
    /// <param name="watch">Whether to take Esc.</param>
    void WatchEscape(bool watch);
}
