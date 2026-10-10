using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The text button of the prototype (docs/07 «Formas»: radius 10; bold 14 px text; optional icon before it), filled
/// as <see cref="Appearance"/> says. It responds on at least 44 × 44 (REG-02): a smaller <c>Width</c> or
/// <c>Height</c> only shrinks the drawing, centered in the target. UI Automation sees a Button whose name is the text,
/// with exactly one pattern (<see cref="TouchButtonAutomationPeer"/>, ACC-001): Invoke; ExpandCollapse when the button
/// opens and closes something and says so with <see cref="IsExpanded"/>; Toggle when it switches a state and says so
/// with <see cref="IsOn"/>.
/// </summary>
/// <remarks>The content is product text, already localized by the view model (CLC0006).</remarks>
public class TouchButton : Button
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(string),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies <see cref="IconSize"/>.</summary>
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize),
        typeof(double),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(20d)
    );

    /// <summary>Identifies <see cref="Appearance"/>.</summary>
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(ButtonAppearance),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(ButtonAppearance.Accent, OnAppearanceChanged)
    );

    /// <summary>Identifies <see cref="IsExpanded"/>.</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool?),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(null, OnIsExpandedChanged)
    );

    /// <summary>Identifies <see cref="IsOn"/>.</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn),
        typeof(bool?),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(null, OnIsOnChanged)
    );

    /// <summary>Default font size of the text (prototype: 14 px).</summary>
    public const double DefaultFontSize = 14;

    static TouchButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TouchButton),
            new FrameworkPropertyMetadata(typeof(TouchButton))
        );
        TemplateProperty.OverrideMetadata(
            typeof(TouchButton),
            new FrameworkPropertyMetadata(ButtonChrome.Template)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(TouchButton),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        PaddingProperty.OverrideMetadata(
            typeof(TouchButton),
            new FrameworkPropertyMetadata(new Thickness(16, 0, 16, 0))
        );
        HorizontalContentAlignmentProperty.OverrideMetadata(
            typeof(TouchButton),
            new FrameworkPropertyMetadata(HorizontalAlignment.Center)
        );
        VerticalContentAlignmentProperty.OverrideMetadata(
            typeof(TouchButton),
            new FrameworkPropertyMetadata(VerticalAlignment.Center)
        );
        TouchTarget.Enforce(typeof(TouchButton));
    }

    /// <summary>Creates a button in its default appearance (accent; ghost for an <see cref="IconButton"/>).</summary>
    public TouchButton()
    {
        ButtonChrome.Apply(this, Appearance);
        FontSize = DefaultFontSize;
        FontWeight = FontWeights.Bold;
    }

    /// <summary>Material Symbols name of the icon before the text; null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Side of the icon in device-independent pixels (20 by default).</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>The fill of the button (<see cref="ButtonAppearance.Accent"/> by default).</summary>
    public ButtonAppearance Appearance
    {
        get => (ButtonAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>
    /// Whether what the button opens is open, for a button that opens and closes a sheet, a menu or a window beside it
    /// (Quick settings, the profile button of the Tab view): UI Automation then sees the ExpandCollapse pattern with
    /// this state instead of Invoke (ACC-001). <see langword="null"/>, the default, for any other button. The state is
    /// the view model's: expanding or collapsing through UI Automation clicks the button.
    /// </summary>
    public bool? IsExpanded
    {
        get => (bool?)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Whether the state the button switches is on (Auto/Fixed, the lock of the Tab view): UI Automation then sees the
    /// Toggle pattern with this state instead of Invoke, so it is not told by color alone (ACC-001, ACC-003).
    /// <see langword="null"/>, the default, for any other button. The state is the view model's: toggling through UI
    /// Automation clicks the button.
    /// </summary>
    public bool? IsOn
    {
        get => (bool?)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>Clicks the button as a tap does; UI Automation's Expand, Collapse and Toggle end here.</summary>
    internal void ClickFromAutomation() => OnClick();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new TouchButtonAutomationPeer(this);

    private static void OnIsExpandedChanged(
        DependencyObject button,
        DependencyPropertyChangedEventArgs e
    ) =>
        (
            UIElementAutomationPeer.FromElement((UIElement)button) as TouchButtonAutomationPeer
        )?.RaiseExpandedChanged((bool?)e.OldValue, (bool?)e.NewValue);

    private static void OnIsOnChanged(
        DependencyObject button,
        DependencyPropertyChangedEventArgs e
    ) =>
        (
            UIElementAutomationPeer.FromElement((UIElement)button) as TouchButtonAutomationPeer
        )?.RaiseOnChanged((bool?)e.OldValue, (bool?)e.NewValue);

    private static void OnAppearanceChanged(
        DependencyObject button,
        DependencyPropertyChangedEventArgs e
    ) => ButtonChrome.Apply((Control)button, (ButtonAppearance)e.NewValue);
}
