using System.Windows;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// The body of a tile (CUA-007, CUA-011): its icon, its name and its key line, one under the other. Nothing is drawn
/// cut: the name takes the lines the height of the tile leaves it, two at most, and ends in «…» when it has more; the
/// key line shows only while it fits under the name.
/// </summary>
/// <remarks>Its three children are, in this order, the icon, the name and the key line.</remarks>
internal sealed class ShortcutTileBody : Panel
{
    /// <summary>Lines the name may take (CUA-011).</summary>
    public const int NameLines = 2;

    private const double Rounding = 0.5;

    private bool _keysFit = true;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count != 3)
        {
            return default;
        }

        var (icon, name, keys) = (InternalChildren[0], InternalChildren[1], InternalChildren[2]);
        var open = new Size(availableSize.Width, double.PositiveInfinity);
        icon.Measure(open);
        name.Measure(open);
        keys.Measure(open);

        var room = availableSize.Height - icon.DesiredSize.Height;
        var keysHeight = keys.DesiredSize.Height;
        var nameHeight = name.DesiredSize.Height;
        if (name is TextBlock text && nameHeight > 0 && LineOf(text) is > 0 and var line)
        {
            // The name first: the lines it has, two at most, of those that fit (one at least).
            var wanted = Math.Clamp((int)Math.Round(nameHeight / line), 1, NameLines);
            var fit = double.IsInfinity(room)
                ? wanted
                : Math.Clamp((int)Math.Floor((room + Rounding) / line), 1, wanted);
            nameHeight = fit * line;
            if (name.DesiredSize.Height > nameHeight + Rounding)
            {
                // The last line that fits ends in «…» (the name trims by characters).
                name.Measure(new Size(availableSize.Width, nameHeight + Rounding));
            }
        }

        _keysFit = keysHeight <= 0 || nameHeight + keysHeight <= room + Rounding;

        var shownKeys = _keysFit ? keys.DesiredSize : default;
        return new Size(
            Math.Max(icon.DesiredSize.Width, Math.Max(name.DesiredSize.Width, shownKeys.Width)),
            icon.DesiredSize.Height + name.DesiredSize.Height + shownKeys.Height
        );
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count != 3)
        {
            return finalSize;
        }

        var keys = InternalChildren[2];
        keys.Opacity = _keysFit ? 1 : 0;
        var y = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            var height = ReferenceEquals(child, keys) && !_keysFit ? 0 : child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, finalSize.Width, height));
            y += height;
        }

        return finalSize;
    }

    private static double LineOf(TextBlock text) =>
        double.IsNaN(text.LineHeight)
            ? text.FontFamily.LineSpacing * text.FontSize
            : text.LineHeight;
}
