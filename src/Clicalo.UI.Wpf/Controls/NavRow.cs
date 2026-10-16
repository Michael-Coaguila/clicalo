using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A navigation row of direction C (D12; «Centro de control» in Quick settings): a card of
/// <see cref="ControlHeight.Large"/> with its line, the icon in a square of <see cref="ControlHeight.Small"/> on
/// <c>accentWash</c> in <c>accentInk</c>, the title (<c>label</c>, one line) with an optional
/// <see cref="Caption"/> under it (<c>caption</c>, two lines at most) and a chevron in <c>muted</c> at the end. It
/// opens another place: UI Automation sees a Button with the Invoke pattern named with the title
/// (<see cref="NavRowAutomationPeer"/>).
/// </summary>
public sealed class NavRow : Button
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(NavRow)
    );

    /// <summary>Identifies <see cref="Caption"/>.</summary>
    public static readonly DependencyProperty CaptionProperty =
        OptionButton.CaptionProperty.AddOwner(typeof(NavRow));

    private const string IconPart = "PART_Icon";
    private const string CaptionPart = "PART_Caption";
    private const string ChevronSymbol = "chevron_right";
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();

    static NavRow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavRow),
            new FrameworkPropertyMetadata(typeof(NavRow))
        );
        TemplateProperty.OverrideMetadata(
            typeof(NavRow),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(NavRow),
            new FrameworkPropertyMetadata(FocusRingStyle.Button)
        );
        TouchTarget.Enforce(typeof(NavRow));
    }

    /// <summary>Creates a navigation row.</summary>
    public NavRow()
    {
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.Text));
        SetResourceReference(FontFamilyProperty, ThemeKeys.UiFont);
        SetResourceReference(FontSizeProperty, ThemeKeys.FontSize(TextStyleToken.Label));
        FontWeight = FontWeights.Bold;
        ControlChrome.SetRadius(this, Radii.Medium);
        MinHeight = ControlHeight.Large;
        Padding = new Thickness(Space.S2, Space.S2, Space.S2, Space.S2);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

    /// <summary>Material Symbols name of the icon at the start.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>What the place holds, under the title; null for none.</summary>
    public string? Caption
    {
        get => (string?)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(ButtonChrome.TextPart) is ContentPresenter presenter)
        {
            presenter.Resources[typeof(TextBlock)] = OptionChrome.Text(
                1,
                centered: false,
                TextStyleToken.Label
            );
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NavRowAutomationPeer(this);

    private static ControlTemplate CreateTemplate()
    {
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(SymbolProperty))
            .With(SymbolIcon.SizeProperty, IconBox.Medium)
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .Paint(SymbolIcon.ForegroundProperty, ColorToken.AccentInk);
        var well = Templates
            .Element<Border>()
            .With(FrameworkElement.WidthProperty, ControlHeight.Small)
            .With(FrameworkElement.HeightProperty, ControlHeight.Small)
            .With(Border.CornerRadiusProperty, new CornerRadius(Radii.Small))
            .With(FrameworkElement.MarginProperty, new Thickness(0, 0, Space.S3, 0))
            .With(DockPanel.DockProperty, Dock.Left)
            .Paint(Border.BackgroundProperty, ColorToken.AccentWash)
            .Add(icon);
        var chevron = Templates
            .Element<SymbolIcon>()
            .With(SymbolIcon.SymbolProperty, ChevronSymbol)
            .With(SymbolIcon.SizeProperty, IconBox.Medium)
            .With(FrameworkElement.MarginProperty, new Thickness(Space.S3, 0, 0, 0))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(DockPanel.DockProperty, Dock.Right)
            .Paint(SymbolIcon.ForegroundProperty, ColorToken.Muted);
        var title = Templates
            .Element<ContentPresenter>(ButtonChrome.TextPart)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        var caption = Templates
            .Element<StyledText>(CaptionPart)
            .With(TextBlock.TextProperty, Templates.Bind(CaptionProperty))
            .With(StyledText.TextStyleProperty, TextStyleToken.Caption)
            .With(StyledText.InkProperty, ColorToken.Muted)
            .With(StyledText.MaxLinesProperty, 2);
        var lines = Templates
            .Element<StackPanel>()
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .Add(title, caption);
        var row = Templates
            .Element<DockPanel>(ControlChrome.ContentPart)
            .With(DockPanel.LastChildFillProperty, true)
            .Add(well, chevron, lines);
        return Templates.Seal(
            typeof(NavRow),
            ControlChrome.Layers(row),
            Templates.When(
                CaptionProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, CaptionPart)
            ),
            Templates.When(
                UIElement.IsMouseOverProperty,
                true,
                Templates.Brush(
                    Border.BackgroundProperty,
                    ColorToken.CardHi,
                    ControlChrome.FillPart
                )
            ),
            Templates.When(UIElement.IsEnabledProperty, false, ControlChrome.Disabled())
        );
    }
}
