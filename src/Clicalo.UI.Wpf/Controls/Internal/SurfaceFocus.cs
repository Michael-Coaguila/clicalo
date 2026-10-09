using System.Windows;
using System.Windows.Input;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// The keyboard focus rule of every base control's automation peer (REG-01, as <c>ShortcutTileAutomationPeer</c>):
/// UI Automation may focus a control only while its window is already active (a lease activated it). Focusing an
/// element of an inactive window would activate the window, and the panel surfaces never take the focus.
/// </summary>
internal static class SurfaceFocus
{
    /// <summary>True when UI Automation may move the keyboard focus to <paramref name="element"/>.</summary>
    public static bool CanFocus(UIElement element) =>
        element.Focusable && element.IsEnabled && Window.GetWindow(element) is { IsActive: true };

    /// <summary>Focuses <paramref name="element"/> inside its active window, or fails without touching the focus.</summary>
    public static void Focus(UIElement element)
    {
        if (!CanFocus(element))
        {
            throw new InvalidOperationException(
                "A control takes the keyboard focus only while its window is active."
            );
        }

        if (!ReferenceEquals(Keyboard.Focus(element), element))
        {
            throw new InvalidOperationException("The control did not accept the keyboard focus.");
        }
    }
}
