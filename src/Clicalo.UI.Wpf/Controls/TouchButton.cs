using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The text button of direction C (D12): one of the four <see cref="ButtonVariant"/>s, in one of the three
/// <see cref="ControlSize"/>s, radius <see cref="Radii.Medium"/>, bold <c>label</c> text in one line with «…»
/// (<see cref="MaxLines"/> 2 where a design asks for it) and an optional icon in a box of
/// <see cref="IconBox.Small"/> before it. It responds on at least 44 × 44 (REG-02): a smaller <c>Width</c> or
/// <c>Height</c> only shrinks the drawing, centered in the target. UI Automation sees a Button whose name is the text,
/// with exactly one pattern (<see cref="TouchButtonAutomationPeer"/>, ACC-001): Invoke; ExpandCollapse when the button
/// opens and closes something and says so with <see cref="IsExpanded"/>; Toggle when it switches a state and says so
/// with <see cref="IsOn"/>, which also draws it pressed exactly as «elegido» (wash, ring of 2 and filled icon).
/// </summary>
/// <remarks>
/// States: under the pointer a step of wash; disabled, no fill, a dashed line and muted text. The button never changes
/// size between states. The content is product text, already localized by the view model (CLC0006).
/// </remarks>
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
        new FrameworkPropertyMetadata(IconBox.Small)
    );

    /// <summary>Identifies <see cref="Variant"/>.</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant),
        typeof(ButtonVariant),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(ButtonVariant.Primary, OnVariantChanged)
    );

    /// <summary>Identifies <see cref="Size"/>.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(ControlSize),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(ControlSize.Small, OnSizeChanged)
    );

    /// <summary>Identifies <see cref="MaxLines"/>.</summary>
    public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register(
        nameof(MaxLines),
        typeof(int),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(1, OnMaxLinesChanged),
        static value => value is 1 or 2
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

    /// <summary>Identifies <see cref="IsActive"/>.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(TouchButton),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Name of the fill of the template (its background in every state).</summary>
    public const string FillPart = ControlChrome.FillPart;

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
            new FrameworkPropertyMetadata(new Thickness(Space.S4, 0, Space.S4, 0))
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

    /// <summary>Creates a primary button of the small height (an <see cref="IconButton"/> is tertiary).</summary>
    public TouchButton()
    {
        ButtonChrome.Apply(this, Variant);
        ControlChrome.SetRadius(this, Radii.Medium);
        Height = ControlSizes.Height(Size);
        SetResourceReference(FontFamilyProperty, ThemeKeys.UiFont);
        SetResourceReference(FontSizeProperty, ThemeKeys.FontSize(TextStyleToken.Label));
        FontWeight = FontWeights.Bold;
    }

    /// <summary>Material Symbols name of the icon before the text; null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Side of the icon box (<see cref="IconBox.Small"/>; <see cref="IconBox.Medium"/> in an icon button).</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Which of the four buttons it is (<see cref="ButtonVariant.Primary"/> by default).</summary>
    public ButtonVariant Variant
    {
        get => (ButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Its height (<see cref="ControlSize.Small"/>, 44, by default); setting it sets <c>Height</c>.</summary>
    public ControlSize Size
    {
        get => (ControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Lines the text of a string content may take before «…»: 1 (the default) or 2.</summary>
    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
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
    /// Whether the state the button switches is on (a mode of the panel header, the lock of the Tab view): UI
    /// Automation then sees the Toggle pattern with this state instead of Invoke, and while it is on the button is
    /// drawn pressed, exactly as «elegido», so it is never told by color alone (ACC-001, ACC-003).
    /// <see langword="null"/>, the default, for any other button. The state is the view model's: toggling through UI
    /// Automation clicks the button.
    /// </summary>
    public bool? IsOn
    {
        get => (bool?)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>
    /// Draws the button pressed, exactly as «elegido», while what it opens or switches is on (Buscar with the search
    /// open, Editar in edit mode, Ajustes rápidos with its sheet open), without changing its UI Automation pattern:
    /// the header then says which mode the panel is in. Use <see cref="IsOn"/> instead when UI Automation must see a
    /// Toggle.
    /// </summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyTextStyle();
    }

    /// <summary>Clicks the button as a tap does; UI Automation's Expand, Collapse and Toggle end here.</summary>
    internal void ClickFromAutomation() => OnClick();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new TouchButtonAutomationPeer(this);

    private void ApplyTextStyle()
    {
        if (GetTemplateChild(ButtonChrome.TextPart) is ContentPresenter presenter)
        {
            presenter.Resources[typeof(TextBlock)] =
                MaxLines == 2 ? ButtonChrome.TwoLines : ButtonChrome.OneLine;
        }
    }

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

    private static void OnVariantChanged(
        DependencyObject button,
        DependencyPropertyChangedEventArgs e
    )
    {
        var control = (TouchButton)button;
        ButtonChrome.Apply(control, (ButtonVariant)e.NewValue);
        if (control is not IconButton)
        {
            var side = (ButtonVariant)e.NewValue == ButtonVariant.Tertiary ? Space.S3 : Space.S4;
            control.Padding = new Thickness(side, 0, side, 0);
        }
    }

    private static void OnSizeChanged(DependencyObject button, DependencyPropertyChangedEventArgs e)
    {
        var control = (TouchButton)button;
        control.Height = ControlSizes.Height((ControlSize)e.NewValue);
        if (control is IconButton)
        {
            control.Width = control.Height;
        }
    }

    private static void OnMaxLinesChanged(
        DependencyObject button,
        DependencyPropertyChangedEventArgs e
    ) => ((TouchButton)button).ApplyTextStyle();
}
