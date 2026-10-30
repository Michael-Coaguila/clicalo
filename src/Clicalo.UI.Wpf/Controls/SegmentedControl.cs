using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A group of equal-width options of which one is chosen (Frecuentes | Word, Vista, Tamaño, Tema; docs/04), in
/// direction C (D12): <see cref="SegmentedItem"/> cells in a grid of <see cref="Columns"/> columns (all in one row
/// when 0). With <see cref="OptionKind.Segment"/> (the default) they sit in a track of
/// <see cref="ControlHeight.Medium"/> on <c>side</c> with its line, <see cref="Space.S1"/> apart; with
/// <see cref="OptionKind.Tile"/> or <see cref="OptionKind.Card"/> they are cards <see cref="Space.S2"/> apart. UI
/// Automation sees a List with the Selection pattern and one ListItem with the SelectionItem pattern per option
/// (ACC-001).
/// </summary>
/// <remarks>
/// Add <see cref="SegmentedItem"/> elements, or data items, which get a <see cref="SegmentedItem"/> container. The
/// chosen option is told by its ring and its ✓ or filled icon, and by UI Automation (ACC-003).
/// </remarks>
public sealed class SegmentedControl : ListBox
{
    /// <summary>Identifies <see cref="Columns"/>.</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns),
        typeof(int),
        typeof(SegmentedControl),
        new FrameworkPropertyMetadata(0, OnColumnsChanged),
        static value => value is int columns && columns >= 0
    );

    /// <summary>Identifies <see cref="Kind"/>.</summary>
    public static readonly DependencyProperty KindProperty = OptionButton.KindProperty.AddOwner(
        typeof(SegmentedControl),
        new FrameworkPropertyMetadata(OptionKind.Segment, OnKindChanged)
    );

    private static readonly ConcurrentDictionary<int, ItemsPanelTemplate> Panels = new();
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();

    static SegmentedControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SegmentedControl),
            new FrameworkPropertyMetadata(typeof(SegmentedControl))
        );
        TemplateProperty.OverrideMetadata(
            typeof(SegmentedControl),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        ItemsPanelProperty.OverrideMetadata(
            typeof(SegmentedControl),
            new FrameworkPropertyMetadata(PanelFor(0))
        );
    }

    /// <summary>Creates an empty group of segments with single selection.</summary>
    public SegmentedControl()
    {
        SelectionMode = SelectionMode.Single;
        Focusable = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Apply(OptionKind.Segment);
    }

    /// <summary>Number of columns; 0 puts every option in one row (the 2 × 2 theme grid has 2).</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// The shape of the options: <see cref="OptionKind.Segment"/> (in a track, the default),
    /// <see cref="OptionKind.Tile"/> or <see cref="OptionKind.Card"/>.
    /// </summary>
    public OptionKind Kind
    {
        get => (OptionKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item) => item is SegmentedItem;

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new SegmentedItem();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is SegmentedItem option)
        {
            option.Kind = Kind;
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new SegmentedControlAutomationPeer(this);

    private void Apply(OptionKind kind)
    {
        if (kind == OptionKind.Segment)
        {
            SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Side));
            SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
            ControlChrome.SetRadius(this, Radii.Medium);
            Padding = new Thickness(Space.S1 / 2);
            Margin = new Thickness(0);
        }
        else
        {
            Background = Brushes.Transparent;
            BorderBrush = Brushes.Transparent;
            Padding = new Thickness(0);
            Margin = new Thickness(-Space.S1);
        }

        foreach (var option in Items.OfType<SegmentedItem>())
        {
            option.Kind = kind;
        }
    }

    private static void OnKindChanged(
        DependencyObject control,
        DependencyPropertyChangedEventArgs e
    ) => ((SegmentedControl)control).Apply((OptionKind)e.NewValue);

    private static void OnColumnsChanged(
        DependencyObject control,
        DependencyPropertyChangedEventArgs e
    ) => ((SegmentedControl)control).ItemsPanel = PanelFor((int)e.NewValue);

    private static ItemsPanelTemplate PanelFor(int columns) =>
        Panels.GetOrAdd(
            columns,
            static count =>
            {
                var grid = Templates.Element<UniformGrid>();
                if (count == 0)
                {
                    grid.SetValue(UniformGrid.RowsProperty, 1);
                }
                else
                {
                    grid.SetValue(UniformGrid.ColumnsProperty, count);
                }

                var template = new ItemsPanelTemplate(grid);
                template.Seal();
                return template;
            }
        );

    private static ControlTemplate CreateTemplate() =>
        Templates.Seal(
            typeof(SegmentedControl),
            ControlChrome.Layers(Templates.Element<ItemsPresenter>(ControlChrome.ContentPart))
        );
}
