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
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The closed handle of the Tab view (PES-001, PES-002, PES-004) in direction C (D12): 44 × 116 on a side edge or
/// 128 × 44 on the top and bottom ones, so it is drawn as deep as it answers, rounded only toward the screen
/// (<see cref="Radii.Medium"/>), with its shadow and a face that lets a little of what is behind show through (D21); a
/// small chevron pointing inside and the mark of Clícalo in <c>accentInk</c> (<see cref="IconBox.Medium"/>), centred,
/// and, in a corner of its own, a <c>warn</c> dot of <see cref="Space.S2"/> while anything is held. A tap opens the bar; a drag along the edge moves it, without opening
/// it (PAN-004). One window per edge, since the shape of its corners is fixed when it is created.
/// </summary>
/// <remarks>
/// REG-02, ACC-002: the window is 44 deep (<see cref="DockGeometry.Handle"/>) and the handle is drawn as deep as the
/// window (<c>sizes.json</c> dock.handleThicknessPx). Should the drawing ever be thinner than the touch, the rest of the
/// window is filled with an almost transparent brush (alpha 1), so a touch there still reaches the handle instead of
/// going through the per-pixel transparent window, and the shadow (0 6 20 at 35 %, PES-001) follows the drawn handle
/// with the depth of that band as an inset (<see cref="NonActivatingWindow.ShadowInset"/>).
/// </remarks>
public sealed class DockHandleWindow : TouchSurface
{
    /// <summary>The mark of Clícalo on the handle, the one of the bubble (BUR-001).</summary>
    private const string BrandSymbol = "touch_app";

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
        Background = SurfaceParts.TouchBand;
        BorderThickness = new Thickness(0);
        // PES-001 (D21): drawn at its whole 44 × 116 (128 × 44). The face lets a little of what is behind show through
        // while its line and its glyphs stay solid, so the folded Tab view is discreet but never lost; the dimming of
        // PES-004 still applies on top.
        var corners = SurfaceLook.DockHandle(side).Corners;
        var face = new Grid
        {
            Width = vertical ? layout.DockHandleThicknessPx : layout.DockHandleHorizontalLengthPx,
            Height = vertical ? layout.DockHandleVerticalLengthPx : layout.DockHandleThicknessPx,
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
        // How much of the face covers what is behind it: the line and the glyphs stay solid.
        var backdrop = new Border { CornerRadius = corners, Opacity = 0.86 };
        backdrop.SetResourceReference(
            Border.BackgroundProperty,
            ThemeBrushKey.For(ColorToken.Panel)
        );
        var outline = new Border { CornerRadius = corners };
        outline.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.Line)
        );
        outline.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        face.Children.Add(backdrop);
        face.Children.Add(outline);

        // The mark of Clícalo, as on the bubble (BUR-001), says what the handle opens, and the small chevron where; the
        // warn dot of anything held goes in a corner of its own, so the pair stays centred whether it shows or not.
        var chevron = SurfaceParts.Icon(
            side switch
            {
                DockSide.Left => "chevron_right",
                DockSide.Top => "expand_more",
                DockSide.Bottom => "expand_less",
                _ => "chevron_left",
            },
            IconBox.Small,
            ColorToken.Muted
        );
        _icon = SurfaceParts.Icon(BrandSymbol, IconBox.Medium, ColorToken.AccentInk);
        _dot = new Ellipse
        {
            Width = Space.S2,
            Height = Space.S2,
            HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            VerticalAlignment = vertical ? VerticalAlignment.Bottom : VerticalAlignment.Center,
            Margin = vertical ? new Thickness(0, 0, 0, Space.S3) : new Thickness(0, 0, Space.S3, 0),
        };
        _dot.SetResourceReference(Shape.FillProperty, ThemeBrushKey.For(ColorToken.Warn));
        var stack = new StackPanel
        {
            Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _icon.Margin = vertical
            ? new Thickness(0, Space.S1, 0, 0)
            : new Thickness(Space.S1, 0, 0, 0);
        _ = stack.Children.Add(chevron);
        _ = stack.Children.Add(_icon);
        face.Children.Add(stack);
        face.Children.Add(_dot);
        var depth = Math.Max(layout.DockHandleThicknessPx, layout.MinTouchTargetPx);
        _button = new TouchButton
        {
            Variant = ButtonVariant.Tertiary,
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
        Announcer = new LiveAnnouncer(_button);
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

    /// <summary>Says what the Tab view keeps off the screen while the bar is closed (a plain confirmation, D21).</summary>
    public LiveAnnouncer Announcer { get; }

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

    private void Refresh()
    {
        // The dot is drawn in its own corner: nothing moves when it appears.
        _dot.Visibility =
            _viewModel.State?.AnythingHeld == true ? Visibility.Visible : Visibility.Collapsed;
        SurfaceParts.Name(_button, _viewModel.Labels.OpenBar);
        Title = _viewModel.Labels.OpenBar;
    }
}
