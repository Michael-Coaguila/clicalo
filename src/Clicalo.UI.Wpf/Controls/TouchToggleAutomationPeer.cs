using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="ToggleSwitch"/> and <see cref="Chip"/> (ACC-001, REG-06): control type Button with
/// the Toggle pattern and its state, the name from <c>AutomationProperties.Name</c> or the text content, and no
/// children. UI Automation can focus it only while its window is active (REG-01).
/// </summary>
/// <param name="owner">The toggle.</param>
public sealed class TouchToggleAutomationPeer(ToggleButton owner)
    : ToggleButtonAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override string GetClassNameCore() => Owner.GetType().Name;

    /// <summary>The toggle is a leaf: its icon, text and track are not separate UI Automation elements.</summary>
    protected override List<AutomationPeer>? GetChildrenCore() => null;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

    /// <inheritdoc />
    protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);
}
