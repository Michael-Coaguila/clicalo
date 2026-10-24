using System.ComponentModel;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Guides;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Bubble;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Guides;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The bubble the panel minimizes to (BUR-001, BUR-002) in direction C (D12): a circle of
/// <see cref="ControlHeight.Large"/> with the <c>panel</c> fill and its decorative line, and the mark of Clícalo, the
/// <c>touch_app</c> hand in <c>accentInk</c> in a box of <see cref="IconBox.Large"/>, so it is never taken for the touch
/// keyboard of Windows; with something held, a ring of <see cref="Stroke.FocusRing"/> in <c>danger</c>. A tap without
/// dragging restores the panel; a drag moves it (PAN-004). Its opacity, never below 55 % and 100 % with panic, is the
/// one <see cref="SurfaceSet"/> gives it (BUR-002).
/// </summary>
public sealed class BubbleWindow : TouchSurface
{
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
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        var mark = new SymbolIcon { Symbol = "touch_app", Size = IconBox.Large };
        mark.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.AccentInk)
        );
        _button = new IconButton
        {
            Content = mark,
            Width = ControlHeight.Large,
            Height = ControlHeight.Large,
            Focusable = false,
            IsTabStop = false,
        };
        _button.Click += (_, _) => _viewModel.Restore();
        GuideTargets.SetTarget(_button, GuideTarget.Bubble);
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
            BorderThickness = new Thickness(Stroke.FocusRing);
        }
        else
        {
            SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
            SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        }
    }
}
