using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A group of equal-width options of which one is selected (prototype quick settings: Vista, Tamaño, Lado and Tema;
/// docs/04): <see cref="SegmentedItem"/> cells in a grid of <see cref="Columns"/> columns (all in one row when 0),
/// 4 px apart, without scrolling. UI Automation sees a List with the Selection pattern and one ListItem with the
/// SelectionItem pattern per option (ACC-001).
/// </summary>
/// <remarks>
/// Add <see cref="SegmentedItem"/> elements, or data items, which get a <see cref="SegmentedItem"/> container. The
/// selected option is told by its fill and by UI Automation (ACC-003).
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

    /// <summary>Creates an empty group with single selection.</summary>
    public SegmentedControl()
    {
        SelectionMode = SelectionMode.Single;
        Margin = new Thickness(-2);
        Focusable = false;
    }

    /// <summary>Number of columns; 0 puts every option in one row (the 2 × 2 theme grid has 2).</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item) => item is SegmentedItem;

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new SegmentedItem();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new SegmentedControlAutomationPeer(this);

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
            Templates
                .Element<Border>()
                .With(Border.PaddingProperty, Templates.Bind(Control.PaddingProperty))
                .Add(Templates.Element<ItemsPresenter>())
        );
}
