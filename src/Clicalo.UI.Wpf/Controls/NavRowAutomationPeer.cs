using System.Windows.Automation.Peers;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="NavRow"/>: a Button with Invoke, named with <c>AutomationProperties.Name</c> or
/// the title, its caption as help text and no children; focusable by UI Automation only while its window is active
/// (REG-01, REG-06).
/// </summary>
/// <param name="owner">The row.</param>
public sealed class NavRowAutomationPeer(NavRow owner) : ButtonAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(NavRow);

    /// <inheritdoc />
    protected override string GetHelpTextCore()
    {
        var help = base.GetHelpTextCore();
        return string.IsNullOrEmpty(help) ? owner.Caption ?? string.Empty : help;
    }

    /// <summary>The row is a leaf: its icon, title and caption are not separate elements.</summary>
    protected override List<AutomationPeer>? GetChildrenCore() => null;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

    /// <inheritdoc />
    protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);
}
