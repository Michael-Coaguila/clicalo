using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;
using GestureTarget = Clicalo.Domain.Touch.TouchTarget;
using TouchTargetSize = Clicalo.UI.Wpf.Controls.TouchTarget;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The minimal panel of M2 (blueprint §8.1, §14): a <see cref="NonActivatingWindow"/> (CLC0001 applies) with the tiles
/// of one profile as real <see cref="ShortcutTile"/>s and, below them while anything is held, the panic strip with
/// «Release all» (SEG-002). It shows what <see cref="PanelViewModel"/> says and forwards what happens:
/// <list type="bullet">
/// <item>finger, pen and mouse go through the product's pointer layer (<see cref="PointerInputSource"/> feeding a
/// <see cref="GestureHost"/>, ADR-0006): an accepted tap, the start and the end of a hold reach the view model with
/// the contact's device and summary;</item>
/// <item>UI Automation Invoke and Toggle of a tile reach it as an invocation (EJE-005, S3);</item>
/// <item>the panic strip and the notices are live regions (assertive and polite, ACC-001).</item>
/// </list>
/// It never takes the foreground (REG-01): it is shown and moved only passively, and hiding it resets the gestures, so
/// a hold under the finger ends with <see cref="HoldEndReason.Reset"/> and the engine releases it (REG-03).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "A WPF window's lifetime ends with Close: OnClosed disposes the pointer layer, the gestures, the theme and the notice timer."
)]
public sealed class PanelWindow : NonActivatingWindow, IPointerFrameSink
{
    private readonly PanelViewModel _viewModel;
    private readonly TimeProvider _time;
    private readonly SizeMetrics _size;
    private readonly ThemeId _theme;
    private readonly UniformGrid _grid;
    private readonly Border _panicStrip;
    private readonly TextBlock _panicText;
    private readonly ShortcutTile _releaseAll;
    private readonly TextBlock _noticeText;
    private readonly List<TileView> _tiles = [];
    private readonly List<Target> _targets = [];
    private readonly Dictionary<uint, TileViewModel> _holds = [];
    private readonly ContactTracker _contacts = new();
    private ThemeScope? _themeScope;
    private GestureHost? _gestures;
    private PointerInputSource? _pointer;
    private LiveAnnouncer? _notices;
    private LiveAnnouncer? _panicAnnouncer;
    private ITimer? _noticeTimer;
    private bool _placed;
    private bool _closed;

    /// <summary>Creates the panel on the UI thread of <paramref name="registry"/> (the Surfaces role).</summary>
    /// <param name="viewModel">What the panel shows.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer frames and the gesture deadlines.</param>
    /// <param name="size">Tile and gap sizes of the panel size in use (<c>data/catalogs/sizes.json</c>).</param>
    /// <param name="columns">Tiles per row (<c>Settings.Columns</c>, 2 to 4).</param>
    /// <param name="theme">The theme the person chose (TEM-001; a Windows contrast theme always wins).</param>
    public PanelWindow(
        PanelViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        SizeMetrics size,
        int columns,
        ThemeId theme
    )
        : base(PanelSurfaceIds.Panel, registry)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        _viewModel = viewModel;
        _time = time;
        _size = size;
        _theme = theme;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = viewModel.AccessibleName;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);

        _grid = new UniformGrid { Columns = columns, Margin = new Thickness(size.GapPx / 2.0) };
        _panicText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(size.GapPx, 0, size.GapPx, 0),
            FontSize = size.TileLabelPx,
        };
        _panicText.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.OnDanger)
        );
        AutomationProperties.SetLiveSetting(_panicText, AutomationLiveSetting.Assertive);
        _releaseAll = new ShortcutTile
        {
            Template = PanicButtonTemplate.Default,
            Pattern = ShortcutTilePattern.Invoke,
            MinWidth = TouchTargetSize.MinimumSize,
            MinHeight = TouchTargetSize.MinimumSize,
            Padding = new Thickness(size.GapPx, 0, size.GapPx, 0),
            Focusable = false,
            IsTabStop = false,
            FontSize = size.TileLabelPx,
        };
        _releaseAll.Invoked += (_, _) => _viewModel.Panic.ReleaseAll();
        var strip = new DockPanel { Margin = new Thickness(size.GapPx / 2.0) };
        DockPanel.SetDock(_releaseAll, Dock.Right);
        strip.Children.Add(_releaseAll);
        strip.Children.Add(_panicText);
        _panicStrip = new Border
        {
            Child = strip,
            CornerRadius = new CornerRadius(Radii.Control),
            Margin = new Thickness(size.GapPx / 2.0, 0, size.GapPx / 2.0, size.GapPx / 2.0),
            Visibility = Visibility.Collapsed,
        };
        _panicStrip.SetResourceReference(
            Border.BackgroundProperty,
            ThemeBrushKey.For(ColorToken.Danger)
        );

        _noticeText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(size.GapPx, 0, size.GapPx, size.GapPx / 2.0),
            FontSize = size.TileLabelPx,
            Visibility = Visibility.Collapsed,
        };
        _noticeText.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );

        // The strip and the notices go below the tiles: the window grows downwards from a fixed top-left corner, so
        // their appearing never moves a tile under a finger that holds it (PAN-009). The final placement, below the
        // header with the layout frozen while contacts last, arrives with the full panel of M3.
        var layout = new StackPanel { Orientation = Orientation.Vertical };
        layout.Children.Add(_grid);
        layout.Children.Add(_panicStrip);
        layout.Children.Add(_noticeText);
        Content = layout;

        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Panic.PropertyChanged += OnPanicChanged;
        _viewModel.Tiles.CollectionChanged += OnTilesChanged;
        LayoutUpdated += (_, _) => RefreshTargets();
        LocationChanged += (_, _) => RefreshTargets();
        RebuildTiles();
        ApplyPanic();
    }

    /// <summary>The tile controls, in display order (desktop tests locate them).</summary>
    public IReadOnlyList<ShortcutTile> TileControls =>
        [.. _tiles.Select(static tile => tile.Control)];

    /// <summary>The «Release all» button of the panic strip.</summary>
    public ShortcutTile ReleaseAllButton => _releaseAll;

    /// <summary>Whether the panic strip is on screen.</summary>
    public bool IsPanicStripVisible => _panicStrip.Visibility == Visibility.Visible;

    /// <summary>The text of the last notice announced.</summary>
    public string NoticeText => _noticeText.Text;

    /// <summary>
    /// Shows or hides the panel as the view model says; the first time, it is placed at its initial position in the
    /// work area of the primary monitor (<c>sizes.json</c>: offsets and margin).
    /// </summary>
    public void Present() => ApplyVisibility();

    /// <summary>
    /// Shows a notice for <c>Timings.Notices.NoticeDuration</c> and announces it (AVI-002, ACC-001): polite for
    /// confirmations, assertive for errors and safety notices.
    /// </summary>
    /// <param name="text">The localized text.</param>
    /// <param name="urgency">How screen readers announce it.</param>
    public void Announce(string text, AnnouncementUrgency urgency)
    {
        ArgumentNullException.ThrowIfNull(text);
        VerifyAccess();
        if (_notices is null || _closed)
        {
            return;
        }

        _noticeText.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _notices.Announce(text, urgency);
        _noticeTimer?.Dispose();
        _noticeTimer = _time.CreateTimer(
            static state => ((PanelWindow)state!).QueueClearNotice(),
            this,
            Timings.Notices.NoticeDuration,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <inheritdoc />
    public void OnFrame(in PointerFrame frame)
    {
        foreach (var sample in frame.Samples)
        {
            _contacts.Observe(sample);
        }

        // The gestures of this frame are delivered before the contacts that ended in it are forgotten, so a tap still
        // has its device and summary.
        _gestures?.OnFrame(frame);
        foreach (var sample in frame.Samples)
        {
            _contacts.Forget(sample);
        }
    }

    /// <inheritdoc />
    public void OnHover(bool inside) { }

    /// <inheritdoc />
    protected override void OnSurfaceInitialized()
    {
        base.OnSurfaceInitialized();
        _themeScope = new ThemeScope(this, _theme);
        _notices = new LiveAnnouncer(_noticeText);
        _panicAnnouncer = new LiveAnnouncer(_panicText);
        var recognizer = new GestureRecognizer(_viewModel.Touch, DpiScale());
        _gestures = new GestureHost(recognizer, Dispatcher, _time, OnGesture);
        _pointer = new PointerInputSource(this, this, _time);
        _pointer.Attach();
        RefreshTargets();
    }

    /// <inheritdoc />
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _gestures?.Recognizer.Configure(_viewModel.Touch, newDpi.DpiScaleX);
        RefreshTargets();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Panic.PropertyChanged -= OnPanicChanged;
        _viewModel.Tiles.CollectionChanged -= OnTilesChanged;
        DetachTiles();
        _noticeTimer?.Dispose();
        _pointer?.Detach();
        _pointer?.Dispose();

        // On the UI thread: a hold that is still active ends with HoldEndReason.Reset and the engine releases it.
        _gestures?.Dispose();
        _themeScope?.Dispose();
        base.OnClosed(e);
    }

    private double DpiScale() => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs change)
    {
        switch (change.PropertyName)
        {
            case nameof(PanelViewModel.IsVisible):
                ApplyVisibility();
                break;
            case nameof(PanelViewModel.AccessibleName):
                Title = _viewModel.AccessibleName;
                break;
            case nameof(PanelViewModel.Touch):
                _gestures?.Recognizer.Configure(_viewModel.Touch, DpiScale());
                break;
        }
    }

    private void OnPanicChanged(object? sender, PropertyChangedEventArgs change) => ApplyPanic();

    private void OnTilesChanged(object? sender, NotifyCollectionChangedEventArgs change) =>
        RebuildTiles();

    private void ApplyVisibility()
    {
        if (_closed)
        {
            return;
        }

        if (_viewModel.IsVisible)
        {
            if (!_placed)
            {
                PlaceInitially();
            }

            ShowPassive();
            RefreshTargets();
            return;
        }

        // A hidden surface receives no pointer-up: end its holds now (REG-03) and forget its contacts.
        _gestures?.Reset();
        _contacts.Clear();
        HidePassive();
    }

    private void ApplyPanic()
    {
        var panic = _viewModel.Panic;
        var wasVisible = _panicStrip.Visibility == Visibility.Visible;
        _releaseAll.AccessibleName = panic.ReleaseAllName;
        _panicStrip.Visibility = panic.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        if (
            panic.IsVisible
            && (
                !wasVisible
                || !string.Equals(_panicText.Text, panic.HeldMessage, StringComparison.Ordinal)
            )
        )
        {
            // SEG-002: announced as an assertive alert whenever what is held changes.
            if (_panicAnnouncer is not null)
            {
                _panicAnnouncer.Announce(panic.HeldMessage, AnnouncementUrgency.Assertive);
            }
            else
            {
                _panicText.Text = panic.HeldMessage;
            }
        }
    }

    private void RebuildTiles()
    {
        DetachTiles();
        _grid.Children.Clear();
        foreach (var viewModel in _viewModel.Tiles)
        {
            var control = new ShortcutTile
            {
                Template = ShortcutTileTemplate.Default,
                Width = _size.TileWidthPx,
                Height = _size.TileHeightPx,
                Margin = new Thickness(_size.GapPx / 2.0),
                Padding = new Thickness(_size.GapPx / 2.0),
                FontSize = _size.TileLabelPx,
                Focusable = false,
                IsTabStop = false,
                Pattern =
                    viewModel.Behavior == TileBehavior.Tap
                        ? ShortcutTilePattern.Invoke
                        : ShortcutTilePattern.Toggle,
            };
            control.Invoked += (_, _) => viewModel.Invoke();
            control.Toggled += (_, _) => viewModel.Invoke();
            PropertyChangedEventHandler handler = (_, _) => Paint(control, viewModel);
            viewModel.PropertyChanged += handler;
            Paint(control, viewModel);
            _tiles.Add(new TileView(viewModel, control, handler));
            _grid.Children.Add(control);
        }

        RefreshTargets();
    }

    private void DetachTiles()
    {
        foreach (var tile in _tiles)
        {
            tile.ViewModel.PropertyChanged -= tile.Handler;
        }

        _tiles.Clear();
        _holds.Clear();
    }

    private static void Paint(ShortcutTile control, TileViewModel viewModel)
    {
        control.AccessibleName = viewModel.AccessibleName;
        control.AccessibleState = viewModel.AccessibleState;
        control.AccessibleHelpText = viewModel.AccessibleHelpText;
        control.ToggleState = viewModel.IsLatched ? ToggleState.On : ToggleState.Off;
    }

    private void RefreshTargets()
    {
        if (_gestures is not { } gestures || PresentationSource.FromVisual(this) is null)
        {
            return;
        }

        _targets.Clear();
        var targets = ImmutableArray.CreateBuilder<GestureTarget>(_tiles.Count + 1);
        foreach (var tile in _tiles)
        {
            Add(
                tile.Control,
                tile.ViewModel.Behavior == TileBehavior.Hold
                    ? TouchTargetKind.Hold
                    : TouchTargetKind.Tap,
                tile.ViewModel
            );
        }

        if (_panicStrip.Visibility == Visibility.Visible)
        {
            Add(_releaseAll, TouchTargetKind.Tap, null);
        }

        gestures.Recognizer.SetTargets(targets.ToImmutable());

        void Add(FrameworkElement element, TouchTargetKind kind, TileViewModel? tile)
        {
            var bounds = PhysicalBounds(element);
            if (bounds.IsEmpty)
            {
                return;
            }

            targets.Add(new GestureTarget(new TouchTargetId(_targets.Count), bounds, kind));
            _targets.Add(new Target(tile));
        }
    }

    private static PhysicalRect PhysicalBounds(FrameworkElement element)
    {
        if (!element.IsVisible || PresentationSource.FromVisual(element) is null)
        {
            return PhysicalRect.Empty;
        }

        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(
            new Point(element.ActualWidth, element.ActualHeight)
        );
        return PhysicalRect.FromEdges(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X),
            (int)Math.Round(bottomRight.Y)
        );
    }

    private void OnGesture(GestureEvent gesture)
    {
        switch (gesture.Kind)
        {
            case GestureKind.Tap when TargetOf(gesture) is { } target:
                if (target.Tile is { } tapped)
                {
                    tapped.Tapped(
                        gesture.PointerId,
                        _contacts.DeviceOf(gesture.PointerId),
                        _contacts.Summarize(gesture.PointerId, gesture.Timestamp, DpiScale()),
                        gesture.Timestamp
                    );
                }
                else
                {
                    _viewModel.Panic.ReleaseAll();
                }

                break;

            case GestureKind.HoldStart when TargetOf(gesture)?.Tile is { } held:
                _holds[gesture.PointerId] = held;
                held.HoldStarted(
                    gesture.PointerId,
                    _contacts.DeviceOf(gesture.PointerId),
                    gesture.Timestamp
                );
                break;

            case GestureKind.HoldEnd:
                // The contact that started the hold owns it (INV-9); even when the tile is gone, the engine releases.
                var holder = _holds.GetValueOrDefault(gesture.PointerId) ?? TargetOf(gesture)?.Tile;
                _holds.Remove(gesture.PointerId);
                holder?.HoldEnded(
                    gesture.PointerId,
                    _contacts.Summarize(gesture.PointerId, gesture.Timestamp, DpiScale()),
                    gesture.HoldEnd
                );
                break;
        }
    }

    private Target? TargetOf(GestureEvent gesture) =>
        gesture.Target is { } id && id.Value >= 0 && id.Value < _targets.Count
            ? _targets[id.Value]
            : null;

    private void PlaceInitially()
    {
        _placed = true;
        if (Content is not UIElement content)
        {
            return;
        }

        // Before the first show: the handle (and so the monitor DPI) exists once MovePassive runs.
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var border = BorderThickness;
        var width = content.DesiredSize.Width + border.Left + border.Right;
        var height = content.DesiredSize.Height + border.Top + border.Bottom;
        var layout = PanelSizes.Layout;
        var work = SystemParameters.WorkArea;
        _ = new WindowInteropHelper(this).EnsureHandle();
        var scale = DpiScale();
        var physicalWidth = (int)Math.Ceiling(width * scale);
        var physicalHeight = (int)Math.Ceiling(height * scale);
        var margin = (int)Math.Round(layout.PanelWorkAreaMarginPx * scale);
        var workLeft = (int)Math.Round(work.Left * scale);
        var workTop = (int)Math.Round(work.Top * scale);
        var workRight = (int)Math.Round(work.Right * scale);
        var workBottom = (int)Math.Round(work.Bottom * scale);
        var left =
            workRight - (int)Math.Round(layout.PanelInitialRightOffsetPx * scale) - physicalWidth;
        var top = workTop + (int)Math.Round(layout.PanelInitialTopPx * scale);
        left = Math.Max(workLeft + margin, left);
        top = Math.Max(workTop + margin, Math.Min(top, workBottom - margin - physicalHeight));
        MovePassive(new PhysicalRect(left, top, physicalWidth, physicalHeight));
    }

    private void QueueClearNotice() =>
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!_closed)
            {
                _noticeText.Visibility = Visibility.Collapsed;
            }
        });

    private sealed record TileView(
        TileViewModel ViewModel,
        ShortcutTile Control,
        PropertyChangedEventHandler Handler
    );

    private sealed record Target(TileViewModel? Tile);
}
