using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The wrap of the prototype (<c>flex-wrap: wrap</c> with <c>flex: 1 1 auto</c>): its children in order, in as few rows
/// as fit, <see cref="Gap"/> apart. The rows are balanced (seven chips go four and three, never six and one alone), and
/// every row is stretched to the whole width, the width left over shared out equally among its children, so each row
/// reaches the edge and the children keep their own height. A child wider than the row gets the row and ends its text
/// in «…».
/// </summary>
internal sealed class JustifiedWrap : Panel
{
    private double[] _natural = [];

    /// <summary>The space between two children and between two rows.</summary>
    public double Gap { get; init; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var count = InternalChildren.Count;
        _natural = new double[count];
        for (var i = 0; i < count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            // A pixel of slack: layout rounding can take a fraction from a text that then ends in «…».
            _natural[i] =
                child.Visibility == Visibility.Collapsed
                    ? 0
                    : child.DesiredSize.Width + Stroke.Line;
        }

        var rows = Rows(availableSize.Width);
        var width = double.IsInfinity(availableSize.Width)
            ? rows.Count == 0
                ? 0
                : RowWidth(rows[0].Start, rows[0].End)
            : availableSize.Width;
        var height = 0d;
        foreach (var (start, end) in rows)
        {
            var widths = Widths(start, end, width);
            var tallest = 0d;
            for (var i = start; i < end; i++)
            {
                InternalChildren[i].Measure(new Size(widths[i - start], availableSize.Height));
                tallest = Math.Max(tallest, InternalChildren[i].DesiredSize.Height);
            }

            height += tallest + (height > 0 ? Gap : 0);
        }

        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;
        foreach (var (start, end) in Rows(finalSize.Width))
        {
            var widths = Widths(start, end, finalSize.Width);
            var tallest = 0d;
            for (var i = start; i < end; i++)
            {
                tallest = Math.Max(tallest, InternalChildren[i].DesiredSize.Height);
            }

            var x = 0d;
            for (var i = start; i < end; i++)
            {
                var child = InternalChildren[i];
                var width = widths[i - start];
                child.Arrange(new Rect(x, y, width, child.DesiredSize.Height));
                x += width + (width > 0 ? Gap : 0);
            }

            y += tallest + Gap;
        }

        return finalSize;
    }

    /// <summary>The children of each row, [start, end), for <paramref name="width"/>.</summary>
    private List<(int Start, int End)> Rows(double width)
    {
        var count = Math.Min(InternalChildren.Count, _natural.Length);
        var rows = new List<(int Start, int End)>();
        if (count == 0)
        {
            return rows;
        }

        if (double.IsInfinity(width))
        {
            rows.Add((0, count));
            return rows;
        }

        // As few rows as fit: each one takes children while they fit.
        var least = 0;
        for (var start = 0; start < count; least++)
        {
            var end = start + 1;
            while (end < count && RowWidth(start, end + 1) <= width)
            {
                end++;
            }

            start = end;
        }

        // Then, with that many rows, the split whose widest row is the narrowest: the rows come out even. On a tie
        // the first rows take more, as a wrap fills them.
        var best = new double[least + 1, count + 1];
        var split = new int[least + 1, count + 1];
        for (var i = 1; i <= count; i++)
        {
            best[1, i] = Cost(0, i, width);
        }

        for (var r = 2; r <= least; r++)
        {
            for (var i = r; i <= count; i++)
            {
                best[r, i] = double.PositiveInfinity;
                for (var s = r - 1; s < i; s++)
                {
                    var widest = Math.Max(best[r - 1, s], Cost(s, i, width));
                    if (widest <= best[r, i])
                    {
                        best[r, i] = widest;
                        split[r, i] = s;
                    }
                }
            }
        }

        var ends = new int[least];
        var last = count;
        for (var r = least; r >= 1; r--)
        {
            ends[r - 1] = last;
            last = r == 1 ? 0 : split[r, last];
        }

        var first = 0;
        foreach (var end in ends)
        {
            rows.Add((first, end));
            first = end;
        }

        return rows;
    }

    /// <summary>
    /// What the row [start, end) costs the split: its natural width, or no split at all when it does not fit, unless it
    /// is a single child, which takes the row whatever its width.
    /// </summary>
    private double Cost(int start, int end, double width)
    {
        var natural = RowWidth(start, end);
        return natural <= width || end - start == 1 ? natural : double.PositiveInfinity;
    }

    /// <summary>The natural width of the children [start, end) side by side, with their gaps.</summary>
    private double RowWidth(int start, int end)
    {
        var width = 0d;
        var shown = 0;
        for (var i = start; i < end; i++)
        {
            if (_natural[i] > 0)
            {
                width += _natural[i];
                shown++;
            }
        }

        return width + (Gap * Math.Max(0, shown - 1));
    }

    /// <summary>The width of each child of the row [start, end): its own, plus an equal share of what is left.</summary>
    private double[] Widths(int start, int end, double width)
    {
        var widths = new double[end - start];
        var shown = 0;
        for (var i = start; i < end; i++)
        {
            shown += _natural[i] > 0 ? 1 : 0;
        }

        var natural = RowWidth(start, end);
        var extra = double.IsInfinity(width) || shown == 0 ? 0 : (width - natural) / shown;
        for (var i = start; i < end; i++)
        {
            widths[i - start] =
                _natural[i] > 0
                    ? Math.Min(
                        Math.Max(0, _natural[i] + extra),
                        double.IsInfinity(width) ? double.MaxValue : width
                    )
                    : 0;
        }

        return widths;
    }
}
