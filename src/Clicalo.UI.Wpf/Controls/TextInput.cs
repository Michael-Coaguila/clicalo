using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The text field of direction C (D12): <see cref="ControlHeight.Medium"/> high (or three lines), radius
/// <see cref="Radii.Medium"/>, <c>field</c> fill with its <c>line</c> drawn inside, <c>body</c> text, an optional
/// leading icon (<see cref="Symbol"/>, the magnifier of a search) and, when <see cref="DictateName"/> is set, the 🎤
/// button beside it: a secondary <see cref="IconButton"/> of <see cref="ControlHeight.Medium"/>,
/// <see cref="Space.S2"/> away, that takes the keyboard to the field and raises <see cref="DictateRequested"/>. While
/// the field has the keyboard it shows the focus ring of 3, 2 outside (TEM-009), however the focus arrived.
/// </summary>
/// <remarks>
/// Changes are reported as they are typed (everything saves itself, REG-07). The view model's text replaces the
/// field's only while the field does not have the keyboard (<see cref="Show"/>), so typing is never overwritten. The
/// placeholder hides while there is text. Texts are product text, already localized (CLC0006).
/// </remarks>
public sealed class TextInput : Grid
{
    private readonly Border _field;
    private readonly StyledText _placeholder;
    private readonly SymbolIcon _icon;
    private bool _applying;
    private bool _warn;

    /// <summary>Creates the field.</summary>
    /// <param name="name">Its accessible name.</param>
    /// <param name="placeholder">Its placeholder; empty for none.</param>
    /// <param name="multiline">Three lines with wrapping and Enter for new lines.</param>
    public TextInput(string name, string placeholder, bool multiline = false)
    {
        IsMultiline = multiline;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var lines = TextStyles.Body.LineHeight * 3;
        Box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, multiline ? Space.S3 : 0, 0, multiline ? Space.S3 : 0),
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? lines + (2 * Space.S3) : ControlHeight.Medium,
            FocusVisualStyle = null,
        };
        Box.SetResourceReference(Control.FontFamilyProperty, ThemeKeys.UiFont);
        Box.SetResourceReference(Control.FontSizeProperty, ThemeKeys.FontSize(TextStyleToken.Body));
        Box.SetResourceReference(Control.ForegroundProperty, ThemeBrushKey.For(ColorToken.Text));
        Box.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, ThemeBrushKey.For(ColorToken.Text));
        AutomationProperties.SetName(Box, name);

        _placeholder = new StyledText(placeholder, TextStyleToken.Body, ColorToken.Muted)
        {
            IsHitTestVisible = false,
            MaxLines = multiline ? 0 : 1,
            Margin = new Thickness(Space.S1 / 2, multiline ? Space.S3 : 0, 0, 0),
            VerticalAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        };
        _icon = new SymbolIcon
        {
            Size = IconBox.Small,
            VerticalAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            Margin = new Thickness(0, multiline ? Space.S3 : 0, Space.S2, 0),
            Visibility = Visibility.Collapsed,
        };
        _icon.SetResourceReference(SymbolIcon.ForegroundProperty, ThemeBrushKey.For(ColorToken.Muted));

        var text = new Grid();
        text.Children.Add(_placeholder);
        text.Children.Add(Box);
        var row = new DockPanel { LastChildFill = true, Margin = new Thickness(Space.S3, 0, Space.S3, 0) };
        DockPanel.SetDock(_icon, Dock.Left);
        row.Children.Add(_icon);
        row.Children.Add(text);
        var layers = new Grid();
        layers.Children.Add(row);
        _field = new Border
        {
            CornerRadius = new CornerRadius(Radii.Medium),
            SnapsToDevicePixels = true,
            Child = layers,
            MinHeight = ControlHeight.Medium,
        };
        _field.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Field));
        _field.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        FocusRing = FieldFocusRing.Attach(layers, Box, Radii.Medium, Stroke.Line);
        Children.Add(_field);

        Dictate = new IconButton
        {
            Symbol = "mic",
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Medium,
            Margin = new Thickness(Space.S2, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };
        SetColumn(Dictate, 1);
        Children.Add(Dictate);
        Dictate.Click += (_, _) =>
        {
            _ = System.Windows.Input.Keyboard.Focus(Box);
            DictateRequested?.Invoke(this, EventArgs.Empty);
        };

        Paint(focused: false);
        Box.TextChanged += (_, _) =>
        {
            _placeholder.Visibility =
                Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (!_applying)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
        Box.GotKeyboardFocus += (_, _) => Paint(focused: true);
        Box.LostKeyboardFocus += (_, _) =>
        {
            Paint(focused: false);
            Left?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Raised when the person changes the text.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the field loses the keyboard.</summary>
    public event EventHandler? Left;

    /// <summary>Raised by 🎤, after the keyboard went to the field: the view starts dictation.</summary>
    public event EventHandler? DictateRequested;

    /// <summary>The text box.</summary>
    public TextBox Box { get; }

    /// <summary>The 🎤 button; visible while <see cref="DictateName"/> is set.</summary>
    public IconButton Dictate { get; }

    /// <summary>The focus ring, visible while the box has the keyboard (TEM-009).</summary>
    public Border FocusRing { get; }

    /// <summary>Whether the field takes three lines.</summary>
    public bool IsMultiline { get; }

    /// <summary>The text.</summary>
    public string Text => Box.Text;

    /// <summary>Material Symbols name of the icon at the start of the field (a search); null for none.</summary>
    public string? Symbol
    {
        get => _icon.Symbol;
        set
        {
            _icon.Symbol = value;
            _icon.Visibility = value is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>The localized name of 🎤 («Dictar el nombre»); null or empty hides it.</summary>
    public string? DictateName
    {
        get => AutomationProperties.GetName(Dictate);
        set
        {
            AutomationProperties.SetName(Dictate, value ?? string.Empty);
            Dictate.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

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

    /// <summary>Draws the line in <c>warnText</c> (an invalid address, EDI-014) or as usual.</summary>
    /// <param name="warn">Whether to warn.</param>
    public void Warn(bool warn)
    {
        _warn = warn;
        Paint(Box.IsKeyboardFocused);
    }

    // The line keeps its thickness, so the ring stays 2 outside it and the text never jumps; its color says warn,
    // focus or rest.
    private void Paint(bool focused) =>
        _field.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(
                _warn ? ColorToken.WarnText
                    : focused ? ColorToken.Accent
                    : ColorToken.Line
            )
        );
}
