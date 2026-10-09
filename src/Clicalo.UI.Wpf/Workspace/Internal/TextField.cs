using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// A text field of the Control Center (docs/05): 44 high (or three lines), <c>field</c> fill, 10 px radius, an accent
/// outline of 2 while it has the keyboard, and a placeholder that hides while there is text. Changes are reported as
/// they are typed (everything saves itself, REG-07). The view model's text replaces the field's only while the field
/// does not have the keyboard, so typing is never overwritten.
/// </summary>
internal sealed class TextField : Border
{
    private readonly TextBlock _placeholder;
    private bool _applying;

    /// <summary>Creates the field.</summary>
    /// <param name="name">Its accessible name.</param>
    /// <param name="placeholder">Its placeholder; empty for none.</param>
    /// <param name="multiline">Three lines with wrapping and Enter for new lines.</param>
    /// <param name="mono">JetBrains Mono, for addresses and programs.</param>
    public TextField(string name, string placeholder, bool multiline = false, bool mono = false)
    {
        CornerRadius = new CornerRadius(10);
        BorderThickness = new Thickness(1);
        SnapsToDevicePixels = true;
        Ui.Ink(this, BackgroundProperty, ColorToken.Field);
        Ui.Ink(this, BorderBrushProperty, ColorToken.Border);
        Box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, multiline ? 8 : 0, 10, multiline ? 8 : 0),
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 84 : 42,
            FocusVisualStyle = null,
        };
        Box.SetResourceReference(TextBox.FontSizeProperty, ThemeKeys.TextSize(15));
        Ui.Ink(Box, TextBox.ForegroundProperty, ColorToken.Text);
        Ui.Ink(Box, TextBox.CaretBrushProperty, ColorToken.Text);
        if (mono)
        {
            Box.SetResourceReference(TextBox.FontFamilyProperty, ThemeKeys.MonoFont);
            Box.FontWeight = AppFonts.MonoWeight;
        }

        AutomationProperties.SetName(Box, name);
        _placeholder = Ui.Text(placeholder, 15, ink: ColorToken.Muted, wrap: multiline);
        _placeholder.IsHitTestVisible = false;
        _placeholder.Margin = new Thickness(13, multiline ? 9 : 0, 13, 0);
        _placeholder.VerticalAlignment = multiline
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
        var layers = new Grid();
        layers.Children.Add(_placeholder);
        layers.Children.Add(Box);
        Child = layers;
        Box.TextChanged += (_, _) =>
        {
            _placeholder.Visibility =
                Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (!_applying)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
        Box.GotKeyboardFocus += (_, _) => Focused(true);
        Box.LostKeyboardFocus += (_, _) =>
        {
            Focused(false);
            Left?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Raised when the person changes the text.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the field loses the keyboard.</summary>
    public event EventHandler? Left;

    /// <summary>The text box.</summary>
    public TextBox Box { get; }

    /// <summary>The text.</summary>
    public string Text => Box.Text;

    /// <summary>Shows <paramref name="text"/> unless the person is typing in the field (or <paramref name="force"/>).</summary>
    /// <param name="text">The text of the view model.</param>
    /// <param name="force">Replace it even while the field has the keyboard.</param>
    public void Show(string text, bool force = false)
    {
        if (
            string.Equals(Box.Text, text, StringComparison.Ordinal)
            || (Box.IsKeyboardFocused && !force)
        )
        {
            return;
        }

        _applying = true;
        try
        {
            Box.Text = text;
            Box.CaretIndex = text.Length;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Changes the placeholder (the language changed).</summary>
    /// <param name="placeholder">The placeholder; empty for none.</param>
    public void SetPlaceholder(string placeholder) => _placeholder.Text = placeholder;

    /// <summary>Paints the outline warn (an invalid address, EDI-014) or as usual.</summary>
    /// <param name="warn">Whether to warn.</param>
    public void Warn(bool warn)
    {
        Ui.Ink(this, BorderBrushProperty, warn ? ColorToken.Warn : ColorToken.Border);
        BorderThickness = new Thickness(warn ? 2 : 1);
    }

    private void Focused(bool focused)
    {
        Ui.Ink(this, BorderBrushProperty, focused ? ColorToken.Accent : ColorToken.Border);
        BorderThickness = new Thickness(focused ? 2 : 1);
    }
}
