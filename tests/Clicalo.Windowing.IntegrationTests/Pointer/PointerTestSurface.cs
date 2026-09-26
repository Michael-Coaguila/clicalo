using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// A panel-like surface for the pointer tests: a <see cref="NonActivatingWindow"/> with four non-focusable tiles in a
/// 2 × 2 grid and a <see cref="PointerInputSource"/> attached in <see cref="OnSurfaceInitialized"/>, as a real surface
/// does. Tile 1 opens a menu on a long press, tile 2 is a hold, tile 3 only taps and tile 4 opens a menu too.
/// </summary>
public sealed class PointerTestSurface : NonActivatingWindow
{
    /// <summary>Logical width.</summary>
    public const double LogicalWidth = 360;

    /// <summary>Logical height.</summary>
    public const double LogicalHeight = 240;

    private static readonly TouchTargetKind[] Kinds =
    [
        TouchTargetKind.TapOrLongPress,
        TouchTargetKind.Hold,
        TouchTargetKind.Tap,
        TouchTargetKind.TapOrLongPress,
    ];

    private readonly Border[] _tiles;

    public PointerTestSurface(SurfaceRegistry registry, IPointerFrameSink sink, TimeProvider clock)
        : base(new SurfaceId(SurfaceKind.Panel, 41), registry)
    {
        Width = LogicalWidth;
        Height = LogicalHeight;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        _tiles =
        [
            .. Kinds.Select(_ => new Border
            {
                Margin = new Thickness(8),
                Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x57, 0x9A)),
                Focusable = false,
            }),
        ];
        var grid = new UniformGrid
        {
            Rows = 2,
            Columns = 2,
            Margin = new Thickness(8),
        };
        foreach (var tile in _tiles)
        {
            grid.Children.Add(tile);
        }

        Content = grid;
        Source = new PointerInputSource(this, sink, clock);
    }

    /// <summary>The pointer source of the surface, attached once its handle exists.</summary>
    public PointerInputSource Source { get; }

    /// <summary>Physical pixels per logical pixel of the surface's monitor.</summary>
    public double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    /// <summary>The bounds of the surface's client area, in physical screen pixels. UI thread only.</summary>
    public PhysicalRect ScreenBounds => ScreenRect(this, ActualWidth, ActualHeight);

    /// <summary>The tiles as recognizer targets (identifiers 1 to 4), in physical screen pixels. UI thread only.</summary>
    public ImmutableArray<TouchTarget> Targets() =>
        [
            .. _tiles.Select(
                (tile, i) =>
                    new TouchTarget(
                        new TouchTargetId(i + 1),
                        ScreenRect(tile, tile.ActualWidth, tile.ActualHeight),
                        Kinds[i]
                    )
            ),
        ];

    /// <inheritdoc />
    protected override void OnSurfaceInitialized() => Source.Attach();

    private static PhysicalRect ScreenRect(Visual element, double width, double height)
    {
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(width, height));
        return PhysicalRect.FromEdges(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X),
            (int)Math.Round(bottomRight.Y)
        );
    }
}
