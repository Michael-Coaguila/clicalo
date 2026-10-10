using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.SystemSection;

/// <summary>
/// A tab of «Sistema» (SIS-001): drawn like the other state buttons of the Control Center, and exposed to UI
/// Automation as a TabItem with the SelectionItem pattern (ACC-001). Its state is the view model's: a tap, or
/// Select from UI Automation, only raises <c>Click</c>. At least 44 × 44 (REG-02).
/// </summary>
internal sealed class SystemTabItem : ToggleButton
{
    static SystemTabItem()
    {
        // Its own style key: no theme style of Windows replaces the template (as CcToggle does).
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SystemTabItem),
            new FrameworkPropertyMetadata(typeof(SystemTabItem))
        );
        TemplateProperty.OverrideMetadata(
            typeof(SystemTabItem),
            new FrameworkPropertyMetadata(CcChrome.ToggleTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(SystemTabItem),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        TouchTarget.Enforce(typeof(SystemTabItem));
    }

    /// <summary>Creates a tab that is not the one in view.</summary>
    public SystemTabItem()
    {
        Cursor = Cursors.Hand;
        IsChecked = false;
    }

    /// <summary>Selects the tab as a tap does (UI Automation's Select).</summary>
    internal void Select() => OnClick();

    /// <inheritdoc />
    /// <remarks>The state follows the view model: a tap only raises <c>Click</c>.</remarks>
    protected override void OnClick() => RaiseEvent(new RoutedEventArgs(ClickEvent, this));

    /// <inheritdoc />
    protected override void OnToggle() => OnClick();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SystemTabItemPeer(this);
}
