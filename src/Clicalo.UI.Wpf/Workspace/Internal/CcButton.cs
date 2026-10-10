using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// A button of the Control Center: <see cref="CcChrome"/>, at least 44 × 44 (REG-02), and the focus ring of docs/07.
/// UI Automation sees a Button named with <c>AutomationProperties.Name</c> (REG-06), with the Invoke pattern or, for
/// the header of a collapsible (<see cref="IsExpanded"/>), with ExpandCollapse (ACC-001).
/// </summary>
internal sealed class CcButton : Button
{
    static CcButton()
    {
        // Its own style key: no theme style of Windows replaces the template (as TouchButton does).
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CcButton),
            new FrameworkPropertyMetadata(typeof(CcButton))
        );
        TemplateProperty.OverrideMetadata(
            typeof(CcButton),
            new FrameworkPropertyMetadata(CcChrome.ButtonTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(CcButton),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        TouchTarget.Enforce(typeof(CcButton));
    }

    /// <summary>Creates a transparent button.</summary>
    public CcButton()
    {
        BorderThickness = new Thickness(1);
        Padding = new Thickness(12, 0, 12, 0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Cursor = Cursors.Hand;
    }

    /// <summary>
    /// Whether what the button opens is open, for the header of a collapsible («Más opciones», a step of a macro):
    /// UI Automation then sees ExpandCollapse with this state instead of Invoke. <see langword="null"/>, the default,
    /// for any other button.
    /// </summary>
    public bool? IsExpanded { get; set; }

    /// <summary>Clicks the button as a tap does; UI Automation's Expand and Collapse end here.</summary>
    internal void ClickFromAutomation() => OnClick();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CcButtonPeer(this);
}
