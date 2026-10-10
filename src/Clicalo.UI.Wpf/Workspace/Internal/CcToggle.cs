using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// A button of the Control Center that shows a state: the chosen section, kind, key, option or switch. Its state is
/// the view model's, never toggled by the tap itself; UI Automation sees that state through the pattern of its
/// <see cref="Role"/> (Toggle, SelectionItem or ExpandCollapse, ACC-001), so the choice is never told by color alone
/// (ACC-003, REG-06). At least 44 × 44 (REG-02).
/// </summary>
internal sealed class CcToggle : ToggleButton
{
    static CcToggle()
    {
        // Its own style key: no theme style of Windows replaces the template (as TouchButton does).
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CcToggle),
            new FrameworkPropertyMetadata(typeof(CcToggle))
        );
        TemplateProperty.OverrideMetadata(
            typeof(CcToggle),
            new FrameworkPropertyMetadata(CcChrome.ToggleTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(CcToggle),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        TouchTarget.Enforce(typeof(CcToggle));
    }

    /// <summary>Creates an unchecked toggle.</summary>
    public CcToggle()
    {
        BorderThickness = new Thickness(1);
        Padding = new Thickness(12, 0, 12, 0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Cursor = Cursors.Hand;
        IsChecked = false;
    }

    /// <summary>What the button is for UI Automation: a switch (the default), a choice of a group or a collapsible.</summary>
    public CcToggleRole Role { get; set; }

    /// <summary>Clicks the button as a tap does; UI Automation's Select, Expand and Collapse end here.</summary>
    internal void ClickFromAutomation() => OnClick();

    /// <inheritdoc />
    /// <remarks>The state follows the view model: a tap only raises <c>Click</c>.</remarks>
    protected override void OnClick() => RaiseEvent(new RoutedEventArgs(ClickEvent, this));

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CcTogglePeer(this);

    /// <inheritdoc />
    /// <remarks>UI Automation's Toggle (voice, Narrator, switches) acts like a tap.</remarks>
    protected override void OnToggle() => OnClick();
}
