using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The CSS grid of the prototype, <c>repeat(auto-fill, minmax(min, 1fr))</c> or <c>repeat(n, 1fr)</c>: as many equal
/// columns as fit with at least <see cref="MinItemWidth"/> (or exactly <see cref="Columns"/>), rows as tall as their
/// tallest item (or <see cref="ItemHeight"/>), <see cref="Gap"/> apart. It never scrolls sideways (CCM-005). Its items
/// are touch targets: where the fixed number of columns would leave them narrower than 44, it uses as many as fit at
/// 44 (REG-02), so a narrow column gets more rows instead of cut buttons.
/// </summary>
internal sealed class AutoFillGrid : Panel
{
    /// <summary>The least width of a column, when <see cref="Columns"/> is 0.</summary>
    public double MinItemWidth { get; set; } = 96;

    /// <summary>A fixed number of columns; 0 for as many as fit.</summary>
    public int Columns { get; set; }

    /// <summary>A fixed height of the items; NaN for their own.</summary>
    public double ItemHeight { get; set; } = double.NaN;

    /// <summary>The space between columns and rows.</summary>
    public double Gap { get; set; } = 8;

    /// <summary>The number of columns for <paramref name="width"/>.</summary>
    public int ColumnsFor(double width)
    {
        if (double.IsInfinity(width) || width <= 0)
        {
            return Math.Max(1, Columns);
        }

        if (Columns > 0)
        {
            var fit = (int)Math.Floor((width + Gap) / (TouchTarget.MinimumSize + Gap));
            return Math.Clamp(fit, 1, Columns);
        }

        return Math.Max(1, (int)Math.Floor((width + Gap) / (MinItemWidth + Gap)));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width)
            ? Math.Max(1, Columns) * (MinItemWidth + Gap)
            : availableSize.Width;
        var columns = ColumnsFor(width);
        var itemWidth = Math.Max(0, (width - (Gap * (columns - 1))) / columns);
        var height = 0d;
        var rowHeight = 0d;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(
                new Size(itemWidth, double.IsNaN(ItemHeight) ? double.PositiveInfinity : ItemHeight)
            );
            rowHeight = Math.Max(
                rowHeight,
                double.IsNaN(ItemHeight) ? child.DesiredSize.Height : ItemHeight
            );
            if (i % columns == columns - 1 || i == InternalChildren.Count - 1)
            {
                height += rowHeight + (height > 0 ? Gap : 0);
                rowHeight = 0;
            }
        }

        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width);
        var itemWidth = Math.Max(0, (finalSize.Width - (Gap * (columns - 1))) / columns);
        var y = 0d;
        for (var start = 0; start < InternalChildren.Count; start += columns)
        {
            var end = Math.Min(start + columns, InternalChildren.Count);
            var rowHeight = 0d;
            for (var i = start; i < end; i++)
            {
                rowHeight = Math.Max(
                    rowHeight,
                    double.IsNaN(ItemHeight) ? InternalChildren[i].DesiredSize.Height : ItemHeight
                );
            }

            for (var i = start; i < end; i++)
            {
                var x = (i - start) * (itemWidth + Gap);
                InternalChildren[i].Arrange(new Rect(x, y, itemWidth, rowHeight));
            }

            y += rowHeight + Gap;
        }

        return finalSize;
    }
}
