using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// One option of a <see cref="SegmentedControl"/> (prototype: 8 px radius, <c>cardHi</c> fill and <c>text</c>; the
/// selected one <c>accent</c> with <c>onAccent</c>, bold 13 px text, optional icon above it). At least 44 × 44
/// (REG-02). UI Automation sees a ListItem with the SelectionItem pattern whose name is the text (ACC-001).
/// </summary>
public sealed class SegmentedItem : ListBoxItem
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(SegmentedItem)
    );

    /// <summary>Name of the rounded border.</summary>
    public const string ChromePart = "PART_Chrome";

    private const string IconPart = "PART_Icon";
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();

    static SegmentedItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SegmentedItem),
            new FrameworkPropertyMetadata(typeof(SegmentedItem))
        );
        TemplateProperty.OverrideMetadata(
            typeof(SegmentedItem),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(SegmentedItem),
            new FrameworkPropertyMetadata(FocusRingStyle.Create(Radii.Control))
        );
        TouchTarget.Enforce(typeof(SegmentedItem));
    }

    /// <summary>Creates an option.</summary>
    public SegmentedItem()
    {
        Templates.UseTheme(this, ColorToken.CardHi, ColorToken.Text, ColorToken.CardHi);
        FontSize = 13;
        FontWeight = FontWeights.Bold;
        Margin = new Thickness(2);
        Padding = new Thickness(6, 4, 6, 4);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

    /// <summary>Material Symbols name of the icon above the text; null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new WrapperPeer(this);

    private static ControlTemplate CreateTemplate()
    {
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(SymbolProperty))
            .With(SymbolIcon.SizeProperty, 20d)
            .With(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 3))
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        var content = Templates
            .Element<ContentPresenter>()
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        var stack = Templates
            .Element<StackPanel>()
            .With(
                FrameworkElement.HorizontalAlignmentProperty,
                Templates.Bind(Control.HorizontalContentAlignmentProperty)
            )
            .With(
                FrameworkElement.VerticalAlignmentProperty,
                Templates.Bind(Control.VerticalContentAlignmentProperty)
            )
            .Add(icon, content);
        var chrome = Templates.Chrome(ChromePart, Radii.Control).Add(stack);
        return Templates.Seal(
            typeof(SegmentedItem),
            chrome,
            Templates.When(
                SymbolProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, IconPart)
            ),
            Templates.When(
                IsSelectedProperty,
                true,
                Templates.Brush(Border.BackgroundProperty, ColorToken.Accent, ChromePart),
                Templates.Brush(Border.BorderBrushProperty, ColorToken.Accent, ChromePart),
                Templates.Brush(TextElement.ForegroundProperty, ColorToken.OnAccent, ChromePart)
            ),
            Templates.When(
                IsEnabledProperty,
                false,
                new Setter(UIElement.OpacityProperty, ButtonChrome.DisabledOpacity, ChromePart)
            )
        );
    }

    /// <summary>
    /// The peer behind the item's ListItem: named by its text when <c>AutomationProperties.Name</c> is not set, and
    /// focusable by UI Automation only while the window is active (REG-01).
    /// </summary>
    private sealed class WrapperPeer(SegmentedItem owner) : ListBoxItemWrapperAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(SegmentedItem);

        protected override string GetNameCore()
        {
            var item = (SegmentedItem)Owner;
            var explicitName = AutomationProperties.GetName(item);
            return !string.IsNullOrEmpty(explicitName)
                ? explicitName
                : item.Content as string ?? string.Empty;
        }

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

        protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);
    }
}
