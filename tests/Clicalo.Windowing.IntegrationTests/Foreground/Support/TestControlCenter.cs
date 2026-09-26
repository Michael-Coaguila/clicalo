using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Clicalo.Application.Ports;

namespace Clicalo.Windowing.IntegrationTests.Foreground.Support;

/// <summary>
/// The Control Center of the S4 lease tests (CCM-004): a normal, activatable window with one text field, shown
/// without activation (<c>ShowActivated = false</c>) and brought to the foreground only by the <c>ControlCenter</c>
/// lease. The flow gives the foreground back BEFORE it closes the window, so Windows never activates another window of
/// this process in between.
/// </summary>
public sealed class TestControlCenter : Window
{
    /// <summary>Creates the window, not shown.</summary>
    public TestControlCenter()
    {
        Title = "Centro de control de prueba";
        Width = 420;
        Height = 200;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Field = new TextBox { MinHeight = 48, Margin = new Thickness(16) };
        AutomationProperties.SetName(Field, "Nombre");
        Content = Field;
    }

    /// <summary>The field (use it on the UI thread).</summary>
    public TextBox Field { get; }

    /// <summary>The window, once shown.</summary>
    public WindowToken Token => new(new WindowInteropHelper(this).Handle);

    /// <summary>True when the field has the keyboard focus (UI thread only).</summary>
    public bool FieldHasKeyboardFocus => ReferenceEquals(Keyboard.FocusedElement, Field);

    /// <summary>Gives the field the keyboard focus, after the lease brought the window to the front.</summary>
    public void FocusField() => _ = Keyboard.Focus(Field);
}
