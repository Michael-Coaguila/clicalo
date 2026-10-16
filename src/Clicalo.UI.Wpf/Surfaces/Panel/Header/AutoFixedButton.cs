using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Header;

/// <summary>
/// The Auto/Fixed button (CAB-003) of direction C (D12): a square of <see cref="ControlHeight.Medium"/> at the end of
/// the row «Qué ver», next to what it controls, with the icon over its word ([auto2] «Auto» or [lockOn2] «Fijo») in
/// <c>accentInk</c>. Auto is the card at rest with <c>autorenew</c>; Fixed is the single look of «elegido» with
/// <c>lock</c> filled, never red (red only means danger). The state is never color alone: the icon, the word, the
/// accessible name and the Toggle pattern say it (ACC-003, REG-06).
/// </summary>
/// <remarks>
/// A tap or a UI Automation Toggle raises <see cref="ToggleRequested"/> instead of flipping the state: the view model
/// decides (PER-006) and its state comes back through <see cref="ToggleButton.IsChecked"/>.
/// </remarks>
public sealed class AutoFixedButton : ToggleButton
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = OptionButton.SymbolProperty.AddOwner(
        typeof(AutoFixedButton)
    );

    private readonly StyledText _label = new(string.Empty, TextStyleToken.LabelSmall, ColorToken.AccentInk)
    {
        MaxLines = 1,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    static AutoFixedButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AutoFixedButton),
            new FrameworkPropertyMetadata(typeof(AutoFixedButton))
        );
        TemplateProperty.OverrideMetadata(
            typeof(AutoFixedButton),
            new FrameworkPropertyMetadata(
                OptionTemplates.For(typeof(AutoFixedButton), OptionKind.Card, IsCheckedProperty)
            )
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(AutoFixedButton),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        TouchTarget.Enforce(typeof(AutoFixedButton));
    }

    /// <summary>Creates the button in Auto.</summary>
    public AutoFixedButton()
    {
        Width = ControlHeight.Medium;
        Height = ControlHeight.Medium;
        Padding = new Thickness(Space.S1);
        IsChecked = false;
        Content = _label;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.AccentInk));
    }

    /// <summary>Raised by a tap or by UI Automation Toggle.</summary>
    public event EventHandler? ToggleRequested;

    /// <summary>Material Symbols name of the icon (<c>autorenew</c> or <c>lock</c>).</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>The word under the icon: «Auto» or «Fijo».</summary>
    public string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <inheritdoc />
    protected override void OnToggle() => ToggleRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new TouchToggleAutomationPeer(this);
}
