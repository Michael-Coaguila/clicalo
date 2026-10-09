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
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Primitives;
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
    Justification = "A WPF window's lifetime ends with Close: OnClosed disposes the pointer layer, the gestures and the timers and detaches the theme."
)]
public sealed class PanelWindow : NonActivatingWindow, IPointerFrameSink
{
    /// <summary>The target identifier of «Release all»; the tiles take the ones after it.</summary>
    private const int ReleaseAllTargetId = 0;

    private readonly PanelViewModel _viewModel;
    private readonly TimeProvider _time;
    private readonly SizeMetrics _size;
    private readonly ThemeService _theme;
    private readonly UniformGrid _grid;
    private readonly Border _panicStrip;
    private readonly TextBlock _panicText;
    private readonly ShortcutTile _releaseAll;
    private readonly TextBlock _noticeText;
    private readonly List<TileView> _tiles = [];
    private readonly Dictionary<int, Target> _targets = [];
    private readonly Dictionary<ShortcutId, int> _targetIds = [];
    private readonly ContactTracker _contacts = new();
    private DimSettings _dim;
    private DateTimeOffset? _lastLeave;
    private bool _hovered;
    private bool _touching;
    private ITimer? _dimTimer;
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
    /// <param name="theme">
    /// The theme service of the UI thread (TEM-001, CUA-011, TEM-006): colors, fonts, text scale and reduce motion.
    /// </param>
    /// <param name="dim">The opacity and the automatic dimming (GEN-009).</param>
    public PanelWindow(
        PanelViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        SizeMetrics size,
        int columns,
        ThemeService theme,
        DimSettings dim
    )
        : base(PanelSurfaceIds.Panel, registry)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        _viewModel = viewModel;
        _time = time;
        _size = size;
        _theme = theme;
        _dim = dim;

        // Rounded with its shadow in a window that never takes a touch (PAN-003, S6); set before the handle exists.
        Look = SurfaceLook.Panel;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = viewModel.AccessibleName;
        theme.Attach(this);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        var labelSize = ThemeKeys.ScaledTextSize(size.TileLabelPx);

        _grid = new UniformGrid { Columns = columns, Margin = new Thickness(size.GapPx / 2.0) };
        _panicText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(size.GapPx, 0, size.GapPx, 0),
        };
        _panicText.SetResourceReference(TextBlock.FontSizeProperty, labelSize);
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
        };
        _releaseAll.SetResourceReference(FontSizeProperty, labelSize);
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
            Visibility = Visibility.Collapsed,
        };
        _noticeText.SetResourceReference(TextBlock.FontSizeProperty, labelSize);
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
        _theme.Changed += OnThemeChanged;
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

    /// <summary>Takes new opacity and dimming settings and applies them at once (GEN-009, AJR-004).</summary>
    /// <param name="dim">The settings.</param>
    public void ApplyDimSettings(DimSettings dim)
    {
        VerifyAccess();
        _dim = dim;
        EvaluateDim();
    }

    /// <inheritdoc />
    public void OnFrame(in PointerFrame frame)
    {
        foreach (var sample in frame.Samples)
        {
            _contacts.Observe(sample);
        }

        TrackTouching();

        // The gestures of this frame are delivered before the contacts that ended in it are forgotten, so a tap still
        // has its device and summary. A gesture handler that throws never leaves an ended contact behind.
        try
        {
            _gestures?.OnFrame(frame);
        }
        finally
        {
            foreach (var sample in frame.Samples)
            {
                _contacts.Forget(sample);
            }

            TrackTouching();
        }
    }

    /// <inheritdoc />
    public void OnHover(bool inside)
    {
        if (_hovered == inside)
        {
            return;
        }

        _hovered = inside;
        if (!inside && !_touching)
        {
            _lastLeave = _time.GetUtcNow();
        }

        EvaluateDim();
    }

    /// <inheritdoc />
    protected override void OnSurfaceInitialized()
    {
        base.OnSurfaceInitialized();
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
        _theme.Changed -= OnThemeChanged;
        DetachTiles();
        _noticeTimer?.Dispose();
        _dimTimer?.Dispose();
        _pointer?.Detach();
        _pointer?.Dispose();

        // On the UI thread: a hold that is still active ends with HoldEndReason.Reset and the engine releases it.
        _gestures?.Dispose();
        _theme.Detach(this);
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

            // It appears awake and dims a while later unless the finger or the pointer is on it (GEN-009).
            _lastLeave = _time.GetUtcNow();
            EvaluateDim();
            ShowPassive();
            RefreshTargets();
            return;
        }

        // A hidden surface receives no pointer-up: end its holds now (REG-03) and forget its contacts.
        _gestures?.Reset();
        _contacts.Clear();
        _touching = false;
        _hovered = false;
        _dimTimer?.Dispose();
        _dimTimer = null;
        HidePassive();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (var tile in _tiles)
        {
            tile.Control.KeysFontSize = KeysFontSize();
        }

        EvaluateDim();
    }

    private double KeysFontSize() => TypeScale.Scale(_size.TileKeysPx, _theme.TextScalePercent);

    /// <summary>Follows whether a finger or the pen is on the panel; lifting the last one counts as leaving it.</summary>
    private void TrackTouching()
    {
        var touching = _contacts.Count > 0;
        if (touching == _touching)
        {
            return;
        }

        _touching = touching;
        if (!touching && !_hovered)
        {
            _lastLeave = _time.GetUtcNow();
        }

        EvaluateDim();
    }

    /// <summary>
    /// Applies the opacity <see cref="DimPolicy"/> decides (GEN-009) and, when the panel will dim later, evaluates
    /// again then. Dimming is only visual: the first touch on a dimmed panel wakes it and acts (EJE-017).
    /// </summary>
    private void EvaluateDim()
    {
        if (_closed)
        {
            return;
        }

        _dimTimer?.Dispose();
        _dimTimer = null;
        var now = _time.GetUtcNow();
        var decision = DimPolicy.Evaluate(
            new DimInputs(
                _dim.AutoDim,
                _dim.Opacity,
                _dim.DimTo,
                DimSurface.Panel,
                _hovered || _touching,
                _lastLeave,
                _viewModel.Panic.IsVisible ? DimExceptions.Panic : DimExceptions.None,
                _theme.ReduceMotion,
                _theme.Effective is ThemeId.HighContrast or ThemeId.SystemHighContrast,
                now
            )
        );
        ApplyDim(decision);
        if (decision.NextEvaluationAt is { } at)
        {
            _dimTimer = _time.CreateTimer(
                static state => ((PanelWindow)state!).QueueEvaluateDim(),
                this,
                at > now ? at - now : TimeSpan.Zero,
                Timeout.InfiniteTimeSpan
            );
        }
    }

    private void QueueEvaluateDim() => _ = Dispatcher.BeginInvoke(EvaluateDim);

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

        // Nothing dims while something is held: «Release all» stays fully visible (SEG-002). Once released, the panel
        // waits the whole delay again before dimming.
        if (panic.IsVisible != wasVisible)
        {
            if (!panic.IsVisible && _lastLeave is not null)
            {
                _lastLeave = _time.GetUtcNow();
            }

            EvaluateDim();
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
                IconSize = _size.TileIconPx,
                KeysFontSize = KeysFontSize(),
                Focusable = false,
                IsTabStop = false,
                Pattern =
                    viewModel.Behavior == TileBehavior.Tap
                        ? ShortcutTilePattern.Invoke
                        : ShortcutTilePattern.Toggle,
            };
            control.SetResourceReference(
                FontSizeProperty,
                ThemeKeys.ScaledTextSize(_size.TileLabelPx)
            );
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
    }

    private static void Paint(ShortcutTile control, TileViewModel viewModel)
    {
        control.AccessibleName = viewModel.AccessibleName;
        control.AccessibleState = viewModel.AccessibleState;
        control.AccessibleHelpText = viewModel.AccessibleHelpText;
        control.ToggleState = viewModel.IsLatched ? ToggleState.On : ToggleState.Off;
        control.Symbol = viewModel.Icon.Length == 0 ? null : viewModel.Icon;
        control.Category = CategoryOf(viewModel.Category);
        control.Badge = viewModel.Badge;

        // CUA-009: a Mantener tile held down shrinks with its outline; a latched toggle shows ACTIVO and its wash.
        control.IsHeld = viewModel.IsLatched && viewModel.Behavior == TileBehavior.Hold;
    }

    /// <summary>The color category of a persisted category id (TEM-003); an unknown one falls back to Edit.</summary>
    private static CategoryToken CategoryOf(string category) =>
        Enum.TryParse<CategoryToken>(category, ignoreCase: true, out var token)
        && Enum.IsDefined(token)
            ? token
            : CategoryToken.Edit;

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
                TargetIdOf(tile.ViewModel.Id),
                tile.ViewModel.Behavior == TileBehavior.Hold
                    ? TouchTargetKind.Hold
                    : TouchTargetKind.Tap,
                tile.ViewModel
            );
        }

        if (_panicStrip.Visibility == Visibility.Visible)
        {
            Add(_releaseAll, ReleaseAllTargetId, TouchTargetKind.Tap, null);
        }

        gestures.Recognizer.SetTargets(targets.ToImmutable());

        void Add(FrameworkElement element, int id, TouchTargetKind kind, TileViewModel? tile)
        {
            var bounds = PhysicalBounds(element);
            if (bounds.IsEmpty)
            {
                return;
            }

            targets.Add(new GestureTarget(new TouchTargetId(id), bounds, kind));
            _targets[id] = new Target(tile);
        }
    }

    /// <summary>
    /// The target identifier of a tile, stable while the panel lives: a contact that went down before the tiles were
    /// rebuilt or reordered keeps its target in the recognizer (PAN-009) and still lifts on the shortcut it touched,
    /// and the filter memory of the recognizer (TAC-002) stays with its tile.
    /// </summary>
    private int TargetIdOf(ShortcutId shortcut)
    {
        if (!_targetIds.TryGetValue(shortcut, out var id))
        {
            id = ReleaseAllTargetId + 1 + _targetIds.Count;
            _targetIds[shortcut] = id;
        }

        return id;
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
                held.HoldStarted(
                    gesture.PointerId,
                    _contacts.DeviceOf(gesture.PointerId),
                    gesture.Timestamp
                );
                break;

            case GestureKind.HoldEnd:
                // The contact that started the hold owns it (INV-9): its end reaches the engine by contact, never
                // through a tile, so it is posted even when the tiles were rebuilt, moved or removed meanwhile.
                _viewModel.HoldEnded(
                    gesture.PointerId,
                    _contacts.Summarize(gesture.PointerId, gesture.Timestamp, DpiScale()),
                    gesture.HoldEnd
                );
                break;
        }
    }

    private Target? TargetOf(GestureEvent gesture) =>
        gesture.Target is { } id ? _targets.GetValueOrDefault(id.Value) : null;

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
