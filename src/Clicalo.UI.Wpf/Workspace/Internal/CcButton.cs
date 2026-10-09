using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// A button of the Control Center: <see cref="CcChrome"/>, at least 44 × 44 (REG-02), and the focus ring of docs/07.
/// UI Automation sees a Button with the Invoke pattern, named with <c>AutomationProperties.Name</c> (REG-06).
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
}
