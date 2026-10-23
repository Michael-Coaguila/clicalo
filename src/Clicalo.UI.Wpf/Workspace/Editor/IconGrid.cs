using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Editor;

/// <summary>
/// The grid of the icon picker (D14): every icon it is given, however many (the whole collection has 3925), in cells of
/// <see cref="Cell"/> that fill the width. It only builds the rows in view and reuses them as it scrolls
/// (a virtualizing stack of rows inside the designed scroll area), so opening a category or typing a letter never
/// builds thousands of buttons. Each cell is an option (SelectionItem) named in the language of the interface; the icon
/// in use has the single look of «elegido».
/// </summary>
internal sealed class IconGrid : ItemsControl
{
    /// <summary>The side of a cell.</summary>
    public const double Cell = ControlHeight.Medium;

    /// <summary>The space between two cells.</summary>
    public const double Gap = Space.S2;

    private IReadOnlyList<string> _icons = [];
    private IReadOnlyList<string> _names = [];
    private HashSet<string> _shared = new(StringComparer.Ordinal);
    private string _current = string.Empty;
    private int _columns;

    /// <summary>Creates an empty grid.</summary>
    public IconGrid()
    {
        Focusable = false;
        ItemsPanel = new ItemsPanelTemplate(
            new FrameworkElementFactory(typeof(VirtualizingStackPanel))
        );
        var scroll = new FrameworkElementFactory(typeof(ThemedScrollViewer));
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        Template = new ControlTemplate(typeof(IconGrid)) { VisualTree = scroll };
        VirtualizingPanel.SetIsVirtualizing(this, true);
        VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Pixel);
        SizeChanged += (_, e) =>
        {
            if (e.WidthChanged)
            {
                Rebuild(force: false);
            }
        };
    }

    /// <summary>What a tap on a cell does: picks its icon.</summary>
    public Action<string>? Pick { get; set; }

    /// <summary>The number of columns that fit now (0 before the first layout).</summary>
    public int Columns => _columns;

    /// <summary>The width of a cell now: <see cref="Cell"/>, or wider so the columns fill the width.</summary>
    internal double CellWidth { get; private set; } = Cell;

    /// <summary>Shows <paramref name="icons"/>, with <paramref name="current"/> marked.</summary>
    /// <param name="icons">The icons, in order.</param>
    /// <param name="names">The name of each icon in the language of the interface, in the same order.</param>
    /// <param name="current">The icon in use.</param>
    public void Show(IReadOnlyList<string> icons, IReadOnlyList<string> names, string current)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(names);
        var same =
            (ReferenceEquals(icons, _icons) || icons.SequenceEqual(_icons, StringComparer.Ordinal))
            && (
                ReferenceEquals(names, _names)
                || names.SequenceEqual(_names, StringComparer.Ordinal)
            );
        var marked = string.Equals(current, _current, StringComparison.Ordinal);
        _icons = icons;
        _names = names;
        _current = current ?? string.Empty;
        if (!same)
        {
            // A name two cells share is told apart by the icon's own name, as help (ACC-001).
            _shared = names
                .GroupBy(static name => name, StringComparer.Ordinal)
                .Where(static group => group.Skip(1).Any())
                .Select(static group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
            Rebuild(force: true);
        }
        else if (!marked)
        {
            foreach (var row in Rows())
            {
                row.Mark(_current);
            }
        }
    }

    /// <summary>Scrolls back to the first row (a new category or a new search starts at the top).</summary>
    public void ScrollToTop()
    {
        if (VisualChildrenCount > 0 && GetVisualChild(0) is ScrollViewer viewer)
        {
            viewer.ScrollToTop();
        }
    }

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new IconGridRow(this);

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item) => item is IconGridRow;

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is IconGridRow row && item is RowCells cells)
        {
            row.Show(cells, _current);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <summary>A cell was tapped.</summary>
    internal void OnPicked(string icon) => Pick?.Invoke(icon);

    private IEnumerable<IconGridRow> Rows()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is IconGridRow row)
            {
                yield return row;
            }
        }
    }

    private void Rebuild(bool force)
    {
        var width = ActualWidth - ComponentSize.ScrollBar;
        var columns = width <= 0 ? 0 : Math.Max(1, (int)Math.Floor((width + Gap) / (Cell + Gap)));
        // The cells share what is left of the width, so the last column ends at the edge of the content and no dead
        // strip is left before the scroll bar; a cell is never narrower than Cell.
        var cellWidth = columns == 0 ? Cell : Math.Max(Cell, ((width + Gap) / columns) - Gap);
        var resized = Math.Abs(cellWidth - CellWidth) > Stroke.Line / 2;
        CellWidth = cellWidth;
        if (!force && columns == _columns)
        {
            if (resized)
            {
                foreach (var row in Rows())
                {
                    row.Resize();
                }
            }

            return;
        }

        _columns = columns;
        if (columns == 0)
        {
            ItemsSource = null;
            return;
        }

        var rows = new List<RowCells>((_icons.Count + columns - 1) / columns);
        for (var start = 0; start < _icons.Count; start += columns)
        {
            var count = Math.Min(columns, _icons.Count - start);
            var icons = new string[count];
            var names = new string[count];
            var helps = new string[count];
            for (var i = 0; i < count; i++)
            {
                icons[i] = _icons[start + i];
                names[i] = start + i < _names.Count ? _names[start + i] : icons[i];
                helps[i] = _shared.Contains(names[i]) ? icons[i].Replace('_', ' ') : string.Empty;
            }

            rows.Add(new RowCells(icons, names, helps));
        }

        ItemsSource = rows;
    }

    /// <summary>The cells of a row: its icons, their names and their help, in the same order.</summary>
    /// <param name="Icons">The Material Symbols names.</param>
    /// <param name="Names">What UI Automation and voice access call each cell.</param>
    /// <param name="Helps">
    /// The Material Symbols name with spaces of a cell whose name another cell of the grid shares; empty otherwise.
    /// </param>
    internal sealed record RowCells(string[] Icons, string[] Names, string[] Helps);

    /// <summary>The grid is one group for UI Automation; its cells, the rows in view, are its options.</summary>
    private sealed class Peer(IconGrid owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(IconGrid);
    }
}
