using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The closed handle of the Tab view (PES-001, PES-002, PES-004): 32 × 116 on a side edge or 128 × 32 on the top and
/// bottom ones, rounded only toward the screen, with a chevron of 22 pointing inside, the icon of the profile in view
/// and a <c>warn</c> dot of 9 when anything is held. A tap opens the bar; a drag along the edge moves it, without opening
/// it (PAN-004). One window per edge, since the shape of its corners is fixed when it is created.
/// </summary>
/// <remarks>
/// REG-02, ACC-002: the window is 44 deep (<see cref="DockGeometry.Handle"/>) and the handle is drawn 32 deep against
/// the screen edge inside it; the rest of the window is filled with an almost transparent brush (alpha 1), so a touch
/// there still reaches the handle instead of going through the per-pixel transparent window. Its shadow (0 6 20 at
/// 35 %, PES-001) follows the drawn handle, not the 44-deep window: the window gives the shadow the shape of the handle
/// and the depth of the touch band as an inset (<see cref="NonActivatingWindow.ShadowInset"/>).
/// </remarks>
public sealed class DockHandleWindow : TouchSurface
{
    private const double ChevronPx = 22;
    private const double IconPx = 20;
    private const double DotPx = 9;

    /// <summary>Paints the touch band around the drawn handle without showing it: alpha 0 would let touches through.</summary>
    private static readonly Brush TouchBand = Frozen(
        new SolidColorBrush(AlmostTransparent(ThemeCatalog.GetPalette(ThemeId.Dark).Shadow))
    );

    private readonly DockBarViewModel _viewModel;
    private readonly TouchButton _button;
    private readonly SymbolIcon _icon;
    private readonly Ellipse _dot;

    /// <summary>Creates the handle of <paramref name="side"/> on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="side">Its edge.</param>
    /// <param name="viewModel">The bar it opens.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public DockHandleWindow(
        DockSide side,
        DockBarViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch
    )
        : base(
            new SurfaceId(SurfaceKind.DockHandle, (int)side),
            registry,
            time,
            theme,
            touch,
            static (_, _, _) => { },
            new SurfaceLook(default, Shadow: null)
        )
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Side = side;
        _viewModel = viewModel;
        var layout = PanelSizes.Layout;
        var vertical = DockGeometry.IsVertical(side);
        var band = Math.Max(0, layout.MinTouchTargetPx - layout.DockHandleThicknessPx);
        ShadowShape = SurfaceLook.DockHandle(side);
        ShadowInset = side switch
        {
            DockSide.Left => new Thickness(0, 0, band, 0),
            DockSide.Top => new Thickness(0, 0, 0, band),
            DockSide.Bottom => new Thickness(0, band, 0, 0),
            _ => new Thickness(band, 0, 0, 0),
        };
        Background = TouchBand;
        BorderThickness = new Thickness(0);
        var face = new Border
        {
            CornerRadius = SurfaceLook.DockHandle(side).Corners,
            Width = vertical ? layout.DockHandleThicknessPx : double.NaN,
            Height = vertical ? double.NaN : layout.DockHandleThicknessPx,
            HorizontalAlignment = side switch
            {
                DockSide.Left => HorizontalAlignment.Left,
                DockSide.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Stretch,
            },
            VerticalAlignment = side switch
            {
                DockSide.Top => VerticalAlignment.Top,
                DockSide.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Stretch,
            },
        };
        face.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        face.SetResourceReference(Border.BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        face.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);

        var chevron = SurfaceParts.Icon(
            side switch
            {
                DockSide.Left => "chevron_right",
                DockSide.Top => "expand_more",
                DockSide.Bottom => "expand_less",
                _ => "chevron_left",
            },
            ChevronPx,
            ColorToken.Accent
        );
        _icon = SurfaceParts.Icon(viewModel.HandleIcon, IconPx, ColorToken.Text);
        _dot = new Ellipse
        {
            Width = DotPx,
            Height = DotPx,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -4, -5, 0),
        };
        _dot.SetResourceReference(Shape.FillProperty, ThemeBrushKey.For(ColorToken.Warn));
        var iconBox = new Grid { VerticalAlignment = VerticalAlignment.Center };
        iconBox.Children.Add(_icon);
        iconBox.Children.Add(_dot);
        var stack = new StackPanel
        {
            Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _ = stack.Children.Add(chevron);
        iconBox.Margin = vertical ? new Thickness(0, 6, 0, 0) : new Thickness(6, 0, 0, 0);
        _ = stack.Children.Add(iconBox);
        face.Child = stack;
        var depth = Math.Max(layout.DockHandleThicknessPx, layout.MinTouchTargetPx);
        _button = new TouchButton
        {
            Appearance = ButtonAppearance.Ghost,
            Content = face,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            MinWidth = 0,
            MinHeight = 0,
            Width = vertical ? depth : layout.DockHandleHorizontalLengthPx,
            Height = vertical ? layout.DockHandleVerticalLengthPx : depth,
            Focusable = false,
            IsTabStop = false,
            Background = Brushes.Transparent,
        };
        _button.Click += (_, _) => _viewModel.OpenBar();
        Content = _button;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Labels.PropertyChanged += OnViewModelChanged;
        Refresh();
    }

    /// <summary>Raised when a drag of the handle passed the threshold.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised while the handle is dragged, with the offset from where the finger went down.</summary>
    public event EventHandler<SurfaceDragEventArgs>? Dragged;

    /// <summary>Raised when the drag ends.</summary>
    public event EventHandler? DragEnded;

    /// <summary>The edge of this handle.</summary>
    public DockSide Side { get; }

    /// <summary>Whether its position is locked (PES-003): a tap still opens the bar, a drag does nothing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>The button that fills the handle.</summary>
    public TouchButton Button => _button;

    /// <inheritdoc />
    protected override IEnumerable<FrameworkElement> DragZones => IsLocked ? [] : [_button];

    /// <inheritdoc />
    protected override bool DragStartsAt(FrameworkElement zone, PhysicalPoint position) =>
        DockGeometry.HandleDragStartsAt(Side, PhysicalBounds(_button, inflate: false), position);

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets() =>
        [SurfaceTarget.Button(_button, _viewModel.OpenBar)];

    /// <inheritdoc />
    protected override void OnDragStarted(FrameworkElement zone) =>
        DragStarted?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override void OnDragged(FrameworkElement zone, PhysicalOffset offset) =>
        Dragged?.Invoke(this, new SurfaceDragEventArgs(offset));

    /// <inheritdoc />
    protected override void OnDragEnded(FrameworkElement zone) =>
        DragEnded?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Labels.PropertyChanged -= OnViewModelChanged;
        base.OnClosed(e);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private static Color AlmostTransparent(Color color) =>
        Color.FromArgb(byte.MinValue + 1, color.R, color.G, color.B);

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private void Refresh()
    {
        _icon.Symbol = _viewModel.HandleIcon;
        _dot.Visibility = SurfaceParts.Shown(_viewModel.State?.AnythingHeld == true);
        SurfaceParts.Name(_button, _viewModel.Labels.OpenBar);
        Title = _viewModel.Labels.OpenBar;
    }
}
