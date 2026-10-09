using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Header;

/// <summary>
/// The Auto/Fixed button of the panel header (CAB-003): drawn at 36 × 36 inside a 44 × 44 target (REG-02), radius 9,
/// with the theme's outline. Auto (off): <c>accentWash</c> fill, <c>accent</c> outline and <c>autorenew</c> icon. Fixed
/// (on): <c>dangerWash</c> fill, <c>danger</c> outline and <c>lock</c> icon. The state is never color alone: the icon,
/// the accessible name and the Toggle pattern say it (ACC-003, REG-06).
/// </summary>
/// <remarks>
/// A tap or a UI Automation Toggle raises <see cref="ToggleRequested"/> instead of flipping the state: the view model
/// decides (PER-006) and its state comes back through <see cref="ToggleButton.IsChecked"/>.
/// </remarks>
public sealed class AutoFixedButton : ToggleButton
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(AutoFixedButton)
    );

    /// <summary>Side of the drawing (prototype: 36).</summary>
    public const double VisualSize = 36;

    /// <summary>Side of the icon (prototype: 17).</summary>
    public const double IconSize = 17;

    /// <summary>Corner radius (prototype: 9).</summary>
    public const double CornerRadius = 9;

    private const string ChromePart = "PART_Chrome";
    private const string IconPart = "PART_Icon";
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();

    static AutoFixedButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AutoFixedButton),
            new FrameworkPropertyMetadata(typeof(AutoFixedButton))
        );
        TemplateProperty.OverrideMetadata(
            typeof(AutoFixedButton),
            new FrameworkPropertyMetadata(DefaultTemplate)
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
        Width = VisualSize;
        Height = VisualSize;
        IsChecked = false;
    }

    /// <summary>Raised by a tap or by UI Automation Toggle.</summary>
    public event EventHandler? ToggleRequested;

    /// <summary>Material Symbols name of the icon (<c>autorenew</c> or <c>lock</c>).</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <inheritdoc />
    protected override void OnToggle() => ToggleRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new TouchToggleAutomationPeer(this);

    private static ControlTemplate CreateTemplate()
    {
        var icon = new FrameworkElementFactory(typeof(SymbolIcon), IconPart);
        icon.SetValue(SymbolIcon.SymbolProperty, new TemplateBindingExtension(SymbolProperty));
        icon.SetValue(SymbolIcon.SizeProperty, IconSize);
        icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );

        var chrome = new FrameworkElementFactory(typeof(Border), ChromePart);
        chrome.SetValue(Border.CornerRadiusProperty, new System.Windows.CornerRadius(CornerRadius));
        chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        chrome.SetResourceReference(
            Border.BackgroundProperty,
            ThemeBrushKey.For(ColorToken.AccentWash)
        );
        chrome.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );
        chrome.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        chrome.AppendChild(icon);

        var box = new FrameworkElementFactory(typeof(TouchTargetBox));
        box.SetValue(
            TouchTargetBox.VisualWidthProperty,
            new TemplateBindingExtension(FrameworkElement.WidthProperty)
        );
        box.SetValue(
            TouchTargetBox.VisualHeightProperty,
            new TemplateBindingExtension(FrameworkElement.HeightProperty)
        );
        box.AppendChild(chrome);

        var template = new ControlTemplate(typeof(AutoFixedButton)) { VisualTree = box };
        var on = new Trigger { Property = IsCheckedProperty, Value = true };
        on.Setters.Add(Brush(Border.BackgroundProperty, ColorToken.DangerWash, ChromePart));
        on.Setters.Add(Brush(Border.BorderBrushProperty, ColorToken.Danger, ChromePart));
        on.Setters.Add(Brush(SymbolIcon.ForegroundProperty, ColorToken.Danger, IconPart));
        template.Triggers.Add(on);
        template.Seal();
        return template;
    }

    private static Setter Brush(DependencyProperty property, ColorToken token, string part) =>
        new(property, new DynamicResourceExtension(ThemeBrushKey.For(token)), part);
}
