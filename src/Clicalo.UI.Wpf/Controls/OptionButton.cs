using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// An option that can be chosen, in direction C (D12): segments, option cards, chips, rows of a list and items of the
/// rail all show «elegido» the same way (wash, a ring of <see cref="Stroke.Selected"/> inside, the icon filled in
/// <c>accentInk</c>, or a ✓ when it has no icon) and keep their size in every state. Its shape is
/// <see cref="Kind"/>. Its state is the view model's (<see cref="ToggleButton.IsChecked"/>): a tap only raises
/// <c>Click</c>. UI Automation sees exactly one pattern, the one of its <see cref="Role"/> (ACC-001), so the choice is
/// never told by color alone (ACC-003, REG-06). At least 44 × 44 (REG-02).
/// </summary>
/// <remarks>Set <c>AutomationProperties.Name</c> when the content is not a plain string (UIA008).</remarks>
public sealed class OptionButton : ToggleButton
{
    /// <summary>Identifies <see cref="Kind"/>.</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(OptionKind),
        typeof(OptionButton),
        new FrameworkPropertyMetadata(OptionKind.Tile, OnKindChanged)
    );

    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(OptionButton)
    );

    /// <summary>Identifies <see cref="Caption"/>.</summary>
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption),
        typeof(string),
        typeof(OptionButton),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies <see cref="Count"/>.</summary>
    public static readonly DependencyProperty CountProperty = Counter.CountProperty.AddOwner(
        typeof(OptionButton)
    );

    /// <summary>Identifies <see cref="IsMonospace"/>.</summary>
    public static readonly DependencyProperty IsMonospaceProperty = DependencyProperty.Register(
        nameof(IsMonospace),
        typeof(bool),
        typeof(OptionButton),
        new FrameworkPropertyMetadata(false, OnTypeChanged)
    );

    /// <summary>Name of the fill of the template (its background in every state).</summary>
    public const string FillPart = ControlChrome.FillPart;

    static OptionButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(OptionButton),
            new FrameworkPropertyMetadata(typeof(OptionButton))
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(OptionButton),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        HorizontalContentAlignmentProperty.OverrideMetadata(
            typeof(OptionButton),
            new FrameworkPropertyMetadata(HorizontalAlignment.Center)
        );
        VerticalContentAlignmentProperty.OverrideMetadata(
            typeof(OptionButton),
            new FrameworkPropertyMetadata(VerticalAlignment.Center)
        );
        TouchTarget.Enforce(typeof(OptionButton));
    }

    /// <summary>Creates a tile option that is not chosen.</summary>
    public OptionButton()
    {
        IsChecked = false;
        Cursor = Cursors.Hand;
        Apply(OptionKind.Tile);
    }

    /// <summary>The shape (<see cref="OptionKind.Tile"/> by default); it sets the height, radius and padding.</summary>
    public OptionKind Kind
    {
        get => (OptionKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Material Symbols name of the icon; null for none (a chosen option without icon shows ✓).</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>The caption under the title of a <see cref="OptionKind.ListRow"/> (for example the app it opens with).</summary>
    public string? Caption
    {
        get => (string?)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>The counter beside the icon of a <see cref="OptionKind.Rail"/> item; 0 hides it.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    /// <summary>True for a key (Ctrl, Alt, a letter): its text goes in JetBrains Mono, the only use of it.</summary>
    public bool IsMonospace
    {
        get => (bool)GetValue(IsMonospaceProperty);
        set => SetValue(IsMonospaceProperty, value);
    }

    /// <summary>What it is for UI Automation (<see cref="OptionRole.Option"/> by default).</summary>
    public OptionRole Role { get; set; }

    /// <summary>Whether it is chosen (the same as <see cref="ToggleButton.IsChecked"/>).</summary>
    public bool IsSelected
    {
        get => IsChecked == true;
        set => IsChecked = value;
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyTextStyle();
    }

    /// <summary>Clicks it as a tap does; UI Automation's Select, Toggle, Expand and Collapse end here.</summary>
    internal void ClickFromAutomation() => OnClick();

    /// <inheritdoc />
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        ApplyTextStyle();
    }

    // Only a string content takes the text style of the option; a view's own content keeps its texts.
    private void ApplyTextStyle()
    {
        SetValue(OptionChrome.IsTextContentProperty, Content is string);
        if (GetTemplateChild(OptionChrome.TextPart) is not ContentPresenter presenter)
        {
            return;
        }

        if (Content is not string)
        {
            presenter.Resources.Remove(typeof(TextBlock));
        }
        else
        {
            presenter.Resources[typeof(TextBlock)] = Kind switch
            {
                OptionKind.Rail => OptionChrome.TextStyleFor(
                    2,
                    centered: true,
                    TextStyleToken.LabelSmall
                ),
                OptionKind.Card => OptionChrome.TextStyleFor(
                    1,
                    centered: true,
                    TextStyleToken.Label
                ),
                OptionKind.ListRow => OptionChrome.TextStyleFor(
                    1,
                    centered: false,
                    TextStyleToken.Label
                ),
                _ => OptionChrome.TextStyleFor(1, centered: true, TextStyleToken.Label),
            };
        }
    }

    /// <inheritdoc />
    /// <remarks>The state follows the view model: a tap only raises <c>Click</c>.</remarks>
    protected override void OnClick() => RaiseEvent(new RoutedEventArgs(ClickEvent, this));

    /// <inheritdoc />
    /// <remarks>UI Automation's Toggle (voice, Narrator) acts like a tap.</remarks>
    protected override void OnToggle() => OnClick();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new OptionButtonAutomationPeer(this);

    private void Apply(OptionKind kind)
    {
        Template = OptionChrome.For(
            typeof(OptionButton),
            kind,
            IsCheckedProperty,
            SymbolProperty,
            CaptionProperty,
            CountProperty
        );
        OptionChrome.Paint(this, kind);
        ApplyType();
        HorizontalContentAlignment =
            kind == OptionKind.ListRow ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
    }

    private void ApplyType()
    {
        var style = Kind == OptionKind.Rail ? TextStyleToken.LabelSmall : TextStyleToken.Label;
        SetResourceReference(FontSizeProperty, ThemeKeys.FontSize(style));
        if (IsMonospace)
        {
            SetResourceReference(FontFamilyProperty, ThemeKeys.MonoFont);
            FontWeight = AppFonts.MonoWeight;
        }
        else
        {
            SetResourceReference(FontFamilyProperty, ThemeKeys.UiFont);
            FontWeight = FontWeights.Bold;
        }
    }

    private static void OnKindChanged(
        DependencyObject option,
        DependencyPropertyChangedEventArgs e
    ) => ((OptionButton)option).Apply((OptionKind)e.NewValue);

    private static void OnTypeChanged(
        DependencyObject option,
        DependencyPropertyChangedEventArgs e
    ) => ((OptionButton)option).ApplyType();
}
