using System.Windows;
using System.Windows.Automation.Peers;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of a container that groups controls under a name, as <see cref="CardAutomationPeer"/> does for a
/// card (ACC-001, REG-06): a Group whose children are the elements of its content, named with
/// <c>AutomationProperties.Name</c>. A Border, a Grid or a StackPanel has no peer of its own, so a name put on one never
/// reached UI Automation; a container whose name must be heard returns this peer. It is never focusable.
/// </summary>
/// <param name="owner">The container.</param>
public sealed class GroupAutomationPeer(FrameworkElement owner)
    : FrameworkElementAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    /// <inheritdoc />
    protected override string GetClassNameCore() => Owner.GetType().Name;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => false;
}
