using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// One option of a <see cref="SegmentedControl"/>, with the single look of «elegido» of direction C (D12): wash, a
/// ring of 2 inside and the icon filled, or a ✓ when it has no icon. Its shape is the group's
/// <see cref="SegmentedControl.Kind"/> (a segment inside the track by default, or tiles and option cards with their
/// line). At least 44 × 44 (REG-02). UI Automation sees a ListItem with the SelectionItem pattern whose name is the
/// text (ACC-001).
/// </summary>
public sealed class SegmentedItem : ListBoxItem
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(SegmentedItem)
    );

    /// <summary>Identifies <see cref="Kind"/>.</summary>
    public static readonly DependencyProperty KindProperty = OptionButton.KindProperty.AddOwner(
        typeof(SegmentedItem),
        new FrameworkPropertyMetadata(OptionKind.Segment, OnKindChanged)
    );

    /// <summary>Name of the fill of the template (its background in every state).</summary>
    public const string FillPart = ControlChrome.FillPart;

    static SegmentedItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SegmentedItem),
            new FrameworkPropertyMetadata(typeof(SegmentedItem))
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(SegmentedItem),
            new FrameworkPropertyMetadata(FocusRingStyle.Create(Radii.Small))
        );
        TouchTarget.Enforce(typeof(SegmentedItem));
    }

    /// <summary>Creates a segment.</summary>
    public SegmentedItem()
    {
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        SetResourceReference(FontFamilyProperty, ThemeKeys.UiFont);
        SetResourceReference(FontSizeProperty, ThemeKeys.FontSize(TextStyleToken.Label));
        FontWeight = FontWeights.Bold;
        Apply(OptionKind.Segment);
    }

    /// <summary>Material Symbols name of the icon; null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>The shape; the group sets it on its options.</summary>
    public OptionKind Kind
    {
        get => (OptionKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(OptionChrome.TextPart) is ContentPresenter presenter)
        {
            presenter.Resources[typeof(TextBlock)] = OptionChrome.Text(
                1,
                centered: true,
                TextStyleToken.Label
            );
        }
    }

    /// <inheritdoc />
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        SetValue(OptionChrome.IsTextContentProperty, newContent is string);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new WrapperPeer(this);

    private void Apply(OptionKind kind)
    {
        Template = OptionChrome.For(
            typeof(SegmentedItem),
            kind,
            IsSelectedProperty,
            SymbolProperty,
            OptionButton.CaptionProperty,
            OptionButton.CountProperty
        );
        OptionChrome.Paint(this, kind);
        Margin = new Thickness(kind == OptionKind.Segment ? Space.S1 / 2 : Space.S1);
    }

    private static void OnKindChanged(
        DependencyObject item,
        DependencyPropertyChangedEventArgs e
    ) => ((SegmentedItem)item).Apply((OptionKind)e.NewValue);

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
