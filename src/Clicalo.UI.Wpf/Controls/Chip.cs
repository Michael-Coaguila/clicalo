using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A toggle chip of the prototype (Teclas fijas Ctrl · Alt · Shift · Win, destination and AI chips): 10 px radius,
/// <c>card</c> fill with a <c>border</c> outline, 13 px text (JetBrains Mono when <see cref="IsMonospace"/>, for
/// keys). On, the fill is <c>accentWash</c>, the outline <c>accent</c> and a check icon leads the text, so the state
/// is never color alone (ACC-003). It responds on at least 44 × 44 (REG-02) and is drawn at its <c>Height</c>
/// (40 by default, as in the prototype). UI Automation sees a Button with the Toggle pattern
/// (<see cref="TouchToggleAutomationPeer"/>, ACC-001).
/// </summary>
public sealed class Chip : ToggleButton
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(Chip)
    );

    /// <summary>Identifies <see cref="IsMonospace"/>.</summary>
    public static readonly DependencyProperty IsMonospaceProperty = DependencyProperty.Register(
        nameof(IsMonospace),
        typeof(bool),
        typeof(Chip),
        new FrameworkPropertyMetadata(false, OnIsMonospaceChanged)
    );

    /// <summary>Height of the drawing (prototype: 40).</summary>
    public const double DefaultHeight = 40;

    /// <summary>Icon shown before the text while the chip is on.</summary>
    public const string CheckedSymbol = "check";

    /// <summary>Name of the rounded border.</summary>
    public const string ChromePart = "PART_Chrome";

    /// <summary>Name of the icon.</summary>
    public const string IconPart = "PART_Icon";

    private const string ContentPart = "PART_Content";
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();

    static Chip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Chip),
            new FrameworkPropertyMetadata(typeof(Chip))
        );
        TemplateProperty.OverrideMetadata(
            typeof(Chip),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(Chip),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        TouchTarget.Enforce(typeof(Chip));
    }

    /// <summary>Creates a chip that is off.</summary>
    public Chip()
    {
        Templates.UseTheme(this, ColorToken.Card, ColorToken.Text, ColorToken.Border);
        Height = DefaultHeight;
        FontSize = 13;
        Padding = new Thickness(10, 0, 10, 0);
        IsChecked = false;
    }

    /// <summary>Material Symbols name of the icon before the text; null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>True to write the text in JetBrains Mono 500, for keys (TEM-005).</summary>
    public bool IsMonospace
    {
        get => (bool)GetValue(IsMonospaceProperty);
        set => SetValue(IsMonospaceProperty, value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new TouchToggleAutomationPeer(this);

    private static void OnIsMonospaceChanged(
        DependencyObject chip,
        DependencyPropertyChangedEventArgs e
    )
    {
        var control = (Chip)chip;
        if ((bool)e.NewValue)
        {
            control.SetResourceReference(FontFamilyProperty, ThemeKeys.MonoFont);
            control.FontWeight = AppFonts.MonoWeight;
        }
        else
        {
            control.SetResourceReference(FontFamilyProperty, ThemeKeys.UiFont);
            control.ClearValue(FontWeightProperty);
        }
    }

    private static ControlTemplate CreateTemplate()
    {
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(SymbolProperty))
            .With(SymbolIcon.SizeProperty, 18d)
            .With(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var content = Templates
            .Element<ContentPresenter>(ContentPart)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        var row = Templates
            .Element<StackPanel>()
            .With(StackPanel.OrientationProperty, Orientation.Horizontal)
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .Add(icon, content);
        var chrome = Templates.Chrome(ChromePart, Radii.Button).Add(row);
        var box = Templates
            .Element<TouchTargetBox>()
            .With(
                TouchTargetBox.VisualWidthProperty,
                Templates.Bind(FrameworkElement.WidthProperty)
            )
            .With(
                TouchTargetBox.VisualHeightProperty,
                Templates.Bind(FrameworkElement.HeightProperty)
            )
            .Add(chrome);

        var off = new MultiTrigger();
        off.Conditions.Add(new Condition(SymbolProperty, null));
        off.Conditions.Add(new Condition(IsCheckedProperty, false));
        off.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, IconPart));

        var on = new MultiTrigger();
        on.Conditions.Add(new Condition(SymbolProperty, null));
        on.Conditions.Add(new Condition(IsCheckedProperty, true));
        on.Setters.Add(new Setter(SymbolIcon.SymbolProperty, CheckedSymbol, IconPart));

        return Templates.Seal(
            typeof(Chip),
            box,
            off,
            on,
            Templates.When(
                IsCheckedProperty,
                true,
                Templates.Brush(Border.BackgroundProperty, ColorToken.AccentWash, ChromePart),
                Templates.Brush(Border.BorderBrushProperty, ColorToken.Accent, ChromePart)
            ),
            Templates.When(
                IsEnabledProperty,
                false,
                new Setter(UIElement.OpacityProperty, ButtonChrome.DisabledOpacity, ChromePart)
            )
        );
    }
}
