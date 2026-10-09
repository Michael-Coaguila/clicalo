using System.ComponentModel;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Bubble;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The 64 px bubble the panel minimizes to (docs/04 «Burbuja minimizada», BUR-001, BUR-002): a circle with the
/// <c>panel</c> fill, a border of 1 and the <c>keyboard</c> icon of 30 in <c>accent</c>; with something held, a red ring
/// of 3. A tap without dragging restores the panel; a drag moves it (PAN-004). Its opacity, never below 55 % and 100 % with
/// panic, is the one <see cref="SurfaceSet"/> gives it (BUR-002).
/// </summary>
public sealed class BubbleWindow : TouchSurface
{
    private const double PanicRingPx = 3;

    private readonly BubbleViewModel _viewModel;
    private readonly IconButton _button;

    /// <summary>Creates the bubble on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="viewModel">What it shows and what a tap does.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public BubbleWindow(
        BubbleViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch
    )
        : base(
            new SurfaceId(SurfaceKind.Bubble, 0),
            registry,
            time,
            theme,
            touch,
            static (_, _, _) => { },
            SurfaceLook.Bubble
        )
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        var layout = PanelSizes.Layout;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        _button = new IconButton
        {
            Symbol = "keyboard",
            IconSize = layout.BubbleIconPx,
            Width = layout.BubbleDiameterPx,
            Height = layout.BubbleDiameterPx,
            Focusable = false,
            IsTabStop = false,
        };
        _button.SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.Accent));
        _button.Click += (_, _) => _viewModel.Restore();
        Content = _button;
        _viewModel.PropertyChanged += OnViewModelChanged;
        Refresh();
    }

    /// <summary>Raised when a drag of the bubble passed the threshold.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised while the bubble is dragged, with the offset from where the finger went down.</summary>
    public event EventHandler<SurfaceDragEventArgs>? Dragged;

    /// <summary>Raised when the drag ends.</summary>
    public event EventHandler? DragEnded;

    /// <summary>The button that fills the bubble.</summary>
    public IconButton Button => _button;

    /// <inheritdoc />
    protected override IEnumerable<FrameworkElement> DragZones => [_button];

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets() =>
        [SurfaceTarget.Button(_button, _viewModel.Restore)];

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
        base.OnClosed(e);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        SurfaceParts.Name(_button, _viewModel.AccessibleName);
        Title = _viewModel.AccessibleName;
        if (_viewModel.IsPanic)
        {
            SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Danger));
            BorderThickness = new Thickness(PanicRingPx);
        }
        else
        {
            SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
            SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        }
    }
}
