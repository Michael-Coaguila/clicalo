using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Foreground.Support;

/// <summary>
/// The search of the S4 lease tests: a real <see cref="NonActivatingWindow"/> with a text field. It is the target of
/// the <c>TextInput</c> lease: only while the lease lasts is it the foreground window and does its field take the
/// keyboard focus (<see cref="FocusField"/>, with <see cref="Keyboard.Focus"/>, BUS-002).
/// </summary>
public sealed class SearchSurface : NonActivatingWindow
{
    /// <summary>Creates the hidden search of <paramref name="width"/> × <paramref name="height"/> logical units.</summary>
    public SearchSurface(SurfaceId id, SurfaceRegistry registry, double width, double height)
        : base(id, registry)
    {
        Width = width;
        Height = height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        Field = new TextBox
        {
            MinHeight = 48,
            FontSize = 22,
            Margin = new Thickness(8),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(Field, "Buscar un atajo");
        Content = Field;
    }

    /// <summary>The text field (use it on the UI thread).</summary>
    public TextBox Field { get; }

    /// <summary>The window as a handle (zero before it exists).</summary>
    public nint Handle => SurfaceWindow.Handle;

    /// <summary>True when the field has the keyboard focus of the surface's thread (UI thread only).</summary>
    public bool FieldHasKeyboardFocus => ReferenceEquals(Keyboard.FocusedElement, Field);

    /// <summary>Gives the field the keyboard focus; effective only while a lease made this window the foreground.</summary>
    public void FocusField()
    {
        _ = Keyboard.Focus(Field);
        Field.CaretIndex = Field.Text.Length;
    }
}
