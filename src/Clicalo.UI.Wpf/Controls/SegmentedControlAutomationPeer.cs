using System.Windows.Automation.Peers;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="SegmentedControl"/> (ACC-001, REG-06): a List with the Selection pattern (one
/// selected option, required) whose children are the options as ListItems with the SelectionItem pattern. UI
/// Automation can focus it only while its window is active (REG-01).
/// </summary>
/// <param name="owner">The segmented control.</param>
public sealed class SegmentedControlAutomationPeer(SegmentedControl owner)
    : ListBoxAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(SegmentedControl);

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

    /// <inheritdoc />
    protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);
}
