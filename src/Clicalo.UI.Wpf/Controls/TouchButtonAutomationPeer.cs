using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="TouchButton"/> and <see cref="IconButton"/> (ACC-001, REG-06): control type Button
/// with the Invoke pattern, the name from <c>AutomationProperties.Name</c> or the text content, and no children (the
/// icon and the text of the template are not separate elements). Like the tiles, UI Automation can focus it only
/// while its window is active (REG-01).
/// </summary>
/// <param name="owner">The button.</param>
public sealed class TouchButtonAutomationPeer(Button owner) : ButtonAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override string GetClassNameCore() => Owner.GetType().Name;

    /// <summary>The button is a leaf: its icon and text are not separate UI Automation elements.</summary>
    protected override List<AutomationPeer>? GetChildrenCore() => null;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

    /// <inheritdoc />
    protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);
}
