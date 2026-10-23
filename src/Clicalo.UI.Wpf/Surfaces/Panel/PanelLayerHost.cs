using System.Windows;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The panel with its layers (direction C, D12: temporary states are layers over the content, never blocks that push
/// it, and the panel has the size of its design). Only <see cref="Content"/> is measured, so the panel keeps the size of
/// its rows whatever its layers show; each layer is laid out afterwards over the region of the content it covers (the
/// body under the header, the status row, the grid…), stretched or held at the bottom of it. A layer that does not fit
/// its region gets the region and scrolls inside, so nothing ever grows the window (no <c>SizeToContent</c> loop).
/// </summary>
/// <remarks>
/// The regions are read from the content just arranged, in this host's coordinates. Layers are added after the content,
/// so they are drawn and hit-tested above it.
/// </remarks>
internal sealed class PanelLayerHost : System.Windows.Controls.Panel
{
    private readonly List<Layer> _layers = [];

    /// <summary>Creates the host of <paramref name="content"/>.</summary>
    /// <param name="content">The rows of the panel, which give it its size.</param>
    public PanelLayerHost(UIElement content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        Children.Add(content);
    }

    /// <summary>The rows of the panel.</summary>
    public UIElement Content { get; }

    /// <summary>The layers, in the order they were added (the last one on top).</summary>
    public IEnumerable<FrameworkElement> Layers => _layers.Select(static layer => layer.Element);

    /// <summary>Adds a layer over <paramref name="region"/>.</summary>
    /// <param name="element">The layer; its own visibility says whether it shows.</param>
    /// <param name="region">The region of the content it covers, in this host's coordinates, once arranged.</param>
    /// <param name="bottom">
    /// Whether it takes only the height it needs, at the bottom of the region (a notice, the tile menu); otherwise it
    /// fills the region.
    /// </param>
    /// <param name="snapTop">
    /// For a layer at the bottom: moves the top it would have up to where it may start (the gap above a row it would
    /// cut), so it covers whole rows; the layer then takes the extra height.
    /// </param>
    public void Add(
        FrameworkElement element,
        Func<Rect> region,
        bool bottom = false,
        Func<double, double>? snapTop = null
    )
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(region);
        _layers.Add(new Layer(element, region, bottom, snapTop));
        Children.Add(element);
    }

    /// <summary>
    /// The region of <paramref name="element"/> of the content in this host's coordinates, from its top to the top of
    /// <paramref name="until"/> when given; empty while it is not laid out.
    /// </summary>
    /// <param name="element">The element whose top (and, without <paramref name="until"/>, whose bottom) bounds the region.</param>
    /// <param name="until">The element whose top ends the region, or <see langword="null"/>.</param>
    public Rect Between(FrameworkElement element, FrameworkElement? until = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!IsAncestorOf(element) || (until is not null && !IsAncestorOf(until)))
        {
            return Rect.Empty;
        }

        var top = element.TranslatePoint(default, this).Y;
        var bottom = until is null
            ? top + element.ActualHeight
            : until.TranslatePoint(default, this).Y;
        return new Rect(0, top, ActualWidth, Math.Max(0, bottom - top));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Content.Measure(availableSize);
        return Content.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        Content.Arrange(new Rect(finalSize));
        foreach (var layer in _layers)
        {
            var element = layer.Element;
            var region = element.Visibility == Visibility.Collapsed ? Rect.Empty : layer.Region();
            if (region.IsEmpty)
            {
                element.Measure(default);
                element.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            region.Intersect(new Rect(finalSize));
            if (region.IsEmpty)
            {
                element.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            element.Measure(region.Size);
            var height = layer.Bottom
                ? Math.Min(region.Height, element.DesiredSize.Height)
                : region.Height;
            var top = region.Bottom - height;
            if (layer.Bottom && layer.SnapTop is { } snap)
            {
                top = Math.Clamp(snap(top), region.Top, top);
            }

            element.Arrange(new Rect(region.X, top, region.Width, region.Bottom - top));
        }

        return finalSize;
    }

    private sealed record Layer(
        FrameworkElement Element,
        Func<Rect> Region,
        bool Bottom,
        Func<double, double>? SnapTop
    );
}
