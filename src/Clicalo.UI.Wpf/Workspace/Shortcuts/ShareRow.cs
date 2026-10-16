using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Shortcuts;

/// <summary>
/// A row that shares its width (direction C, D12): every child gets the width it asks for, <see cref="Gap"/> apart.
/// When they do not fit, the children that <see cref="GetCanShrink">can shrink</see> give up width in proportion to
/// their size and end their texts in «…» (the chips of «Tu panel muestra»); the others keep theirs. With
/// <see cref="Fill"/>, the width that is left over is shared out equally (the fixed footer of the editor), so the
/// buttons fill the row without any of them cutting its text while another has room to spare.
/// </summary>
internal sealed class ShareRow : Panel
{
    /// <summary>Identifies the attached <c>CanShrink</c> property (true by default).</summary>
    public static readonly DependencyProperty CanShrinkProperty =
        DependencyProperty.RegisterAttached(
            "CanShrink",
            typeof(bool),
            typeof(ShareRow),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure
            )
        );

    private double[] _natural = [];

    /// <summary>The space between two children.</summary>
    public double Gap { get; init; }

    /// <summary>Whether the width left over is shared out among the children.</summary>
    public bool Fill { get; init; }

    /// <summary>Whether <paramref name="element"/> gives up width when the row is short.</summary>
    /// <param name="element">A child.</param>
    public static bool GetCanShrink(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(CanShrinkProperty);
    }

    /// <summary>Lets <paramref name="element"/> give up width when the row is short, or not.</summary>
    /// <param name="element">A child.</param>
    /// <param name="value">Whether it can shrink.</param>
    public static void SetCanShrink(UIElement element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(CanShrinkProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0d;
        _natural = new double[InternalChildren.Count];
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            // A pixel of slack: layout rounding can take a fraction from a text that then ends in «…».
            _natural[i] = child.DesiredSize.Width + Stroke.Line;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        var widths = Widths(availableSize.Width);
        var total = 0d;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(widths[i], availableSize.Height));
            total += widths[i];
        }

        total += Gap * Math.Max(0, InternalChildren.Count - 1);
        return new Size(
            double.IsInfinity(availableSize.Width) ? total : Math.Min(total, availableSize.Width),
            height
        );
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = Widths(finalSize.Width);
        var x = 0d;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Arrange(new Rect(x, 0, widths[i], finalSize.Height));
            x += widths[i] + Gap;
        }

        return finalSize;
    }

    private double[] Widths(double available)
    {
        var count = Math.Min(InternalChildren.Count, _natural.Length);
        var widths = new double[InternalChildren.Count];
        var wanted = 0d;
        var shrinkable = 0d;
        for (var i = 0; i < count; i++)
        {
            var child = InternalChildren[i];
            widths[i] = _natural[i];
            wanted += widths[i];
            if (GetCanShrink(child))
            {
                shrinkable += widths[i];
            }
        }

        if (double.IsInfinity(available) || count == 0)
        {
            return widths;
        }

        // Layout rounding can leave the row a fraction of a pixel short of what it measured: that is no reason to cut.
        var room = available - (Gap * (count - 1)) + Stroke.Line;
        if (wanted <= room)
        {
            if (Fill)
            {
                var extra = Math.Max(0, room - Stroke.Line - wanted) / count;
                for (var i = 0; i < count; i++)
                {
                    widths[i] += extra;
                }
            }

            return widths;
        }

        // The widest give up width first, down to a common width, so a short chip keeps all of its text while a long
        // one ends in «…».
        var missing = wanted - room;
        var shrinkers = Enumerable
            .Range(0, count)
            .Where(i => GetCanShrink(InternalChildren[i]))
            .OrderByDescending(i => widths[i])
            .ToList();
        var target = shrinkable - missing;
        if (shrinkers.Count == 0 || target <= 0)
        {
            foreach (var i in shrinkers)
            {
                widths[i] = 0;
            }

            return widths;
        }

        // The level L with Σ min(width, L) = target.
        var level = 0d;
        var rest = target;
        for (var k = shrinkers.Count - 1; k >= 0; k--)
        {
            var candidate = rest / (k + 1);
            var smallest = widths[shrinkers[k]];
            if (smallest >= candidate)
            {
                level = candidate;
                break;
            }

            rest -= smallest;
            level = candidate;
        }

        foreach (var i in shrinkers)
        {
            widths[i] = Math.Min(widths[i], level);
        }

        return widths;
    }
}
