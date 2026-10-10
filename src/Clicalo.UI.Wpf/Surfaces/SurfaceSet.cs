using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Bubble;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using Microsoft.Win32;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// Every surface of the panel on the UI thread of the Surfaces role (blueprint §8.1): the panel, the bubble, the handle
/// and the bar of the Tab view with the windows beside it (Pinned, the profile grid, sticky keys, Quick settings, the
/// menu of a shortcut and the notice surface), and the floating «Release all». It shows exactly the form the
/// composition decides (PAN-001), places each surface in physical pixels inside the work area of its monitor (PAN-002,
/// PAN-006, <see cref="PanelGeometry"/>, <see cref="DockGeometry"/>), moves the panel, the bubble and the handle when
/// they are dragged and reports where they end, places everything again on any change of size, display, scale or work
/// area, moves the panel away from the touch keyboard (BUS-002), follows the finger and the pointer on every surface
/// and gives each its opacity (<see cref="SurfaceDimmer"/>, GEN-009). It decides no rule of the product.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The dimmer is disposed in Dispose, which the panel's Closed calls."
)]
public sealed class SurfaceSet : IDisposable
{
    private readonly PanelWindow _panel;
    private readonly SurfaceDimmer _dimmer;
    private readonly ThemeService _theme;
    private readonly TimeProvider _time;
    private readonly SurfaceRegistry _registry;
    private readonly DockBarViewModel _dock;
    private readonly PanelViewModel _panelModel;
    private readonly Action<uint, ContactSummary, HoldEndReason> _holdEnded;
    private readonly Action<MonitorPosition> _savePosition;
    private readonly Action<string, DockSide, int> _saveHandle;
    private readonly Action<DockTileViewModel, bool> _tileUsed;
    private readonly Action _holdReleased;
    private readonly BubbleWindow _bubble;
    private readonly PanicPillWindow _panicPill;
    private readonly Dictionary<DockFlyout, DockFlyoutWindow> _flyouts = [];
    private readonly DockCoachWindow _coach;
    private readonly DockNoticeWindow _notice;
    private readonly DockMenuWindow? _menu;
    private readonly DockQuickWindow? _quick;
    private readonly Dictionary<DockSide, DockHandleWindow> _handles = [];
    private readonly Dictionary<DockSide, DockBarWindow> _bars = [];
    private readonly List<TouchSurface> _surfaces = [];
    private ImmutableArray<DisplayMonitor> _monitors;
    private SurfaceLayout? _layout;
    private TouchSettings _touch;
    private string? _monitorId;
    private PhysicalPoint? _topLeft;
    private PhysicalPoint? _beforeKeyboard;
    private PhysicalRect _occluded;
    private PhysicalRect _dragOrigin;
    private int _handleStart;
    private int? _handleDrag;
    private bool _started;
    private bool _reflowing;
    private bool _reflowQueued;
    private bool _disposed;

    /// <summary>Creates the surfaces around <paramref name="panel"/> on its UI thread.</summary>
    /// <param name="panel">The panel window.</param>
    /// <param name="panelModel">The panel: its profile grid and sticky keys go in the windows beside the bar.</param>
    /// <param name="dock">The handle and the bar.</param>
    /// <param name="bubble">The bubble.</param>
    /// <param name="dimming">The opacity of every surface and the finger on them (GEN-009).</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    /// <param name="time">The clock of the pointer layer and of the dimming.</param>
    /// <param name="touch">The touch filter.</param>
    /// <param name="callbacks">Where releases, holds, uses and positions go.</param>
    /// <param name="layers">
    /// Quick settings, the menu of a shortcut and test mode, shared with the panel: beside the bar they get windows of
    /// their own (PES-009, PES-010, PES-014). <see langword="null"/> for a set without them.
    /// </param>
    public SurfaceSet(
        PanelWindow panel,
        PanelViewModel panelModel,
        DockBarViewModel dock,
        BubbleViewModel bubble,
        SurfaceDimming dimming,
        SurfaceRegistry registry,
        ThemeService theme,
        TimeProvider time,
        TouchSettings touch,
        SurfaceCallbacks callbacks,
        PanelLayerModels? layers = null
    )
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(panelModel);
        ArgumentNullException.ThrowIfNull(dock);
        ArgumentNullException.ThrowIfNull(bubble);
        ArgumentNullException.ThrowIfNull(dimming);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(callbacks);
        _panel = panel;
        _panelModel = panelModel;
        _dock = dock;
        _dimmer = new SurfaceDimmer(dimming, theme, panel.Dispatcher);
        _registry = registry;
        _theme = theme;
        _time = time;
        _touch = touch;
        _holdEnded = callbacks.HoldEnded;
        _savePosition = callbacks.SavePosition;
        _saveHandle = callbacks.SaveHandle;
        _tileUsed = callbacks.TileUsed;
        _holdReleased = callbacks.HoldReleased;
        _monitors = DisplayMonitors.Snapshot();

        _bubble = Track(new BubbleWindow(bubble, registry, time, theme, touch), DimSurface.Bubble);
        _bubble.DragStarted += (_, _) => _dragOrigin = _bubble.ScreenBounds;
        _bubble.Dragged += (_, drag) => MoveDragged(_bubble, drag.Offset);
        _bubble.DragEnded += (_, _) => SaveDragged(_bubble.ScreenBounds);
        _panicPill = Track(
            new PanicPillWindow(registry, time, theme, touch, callbacks.ReleaseAll),
            kind: null
        );
        foreach (var flyout in new[] { DockFlyout.Pinned, DockFlyout.Profiles, DockFlyout.Sticky })
        {
            var window = Track(
                new DockFlyoutWindow(
                    flyout,
                    dock,
                    panelModel,
                    registry,
                    time,
                    theme,
                    touch,
                    _holdEnded
                ),
                DimSurface.Dock
            );
            window.TileTapped += (_, used) => _tileUsed(used.Tile, true);
            window.HoldReleased += (_, _) => _holdReleased();
            window.SizeChanged += (_, _) => Reflow();
            _flyouts[flyout] = window;
        }

        _coach = Track(new DockCoachWindow(dock, registry, time, theme, touch), DimSurface.Dock);
        _coach.SizeChanged += (_, _) => Reflow();

        // PES-014: the notices of the Tab view never dim, like the floating «Release all».
        _notice = Track(
            new DockNoticeWindow(
                panelModel,
                layers?.TestMode,
                dock.Labels,
                registry,
                time,
                theme,
                touch
            ),
            kind: null
        );
        _notice.SizeChanged += (_, _) => Reflow();
        if (layers is not null)
        {
            _menu = Track(
                new DockMenuWindow(layers.Menu, registry, time, theme, touch),
                DimSurface.Dock
            );
            _menu.SizeChanged += (_, _) => Reflow();
            _quick = Track(
                new DockQuickWindow(layers.QuickSettings, registry, time, theme, touch),
                DimSurface.Dock
            );
            _quick.SizeChanged += (_, _) => Reflow();
        }

        panel.Placer = PlacePanel;
        _dimmer.Follow(panel);
        panel.DragStarted += (_, _) => _dragOrigin = _panel.ScreenBounds;
        panel.Dragged += (_, drag) => MoveDragged(_panel, drag.Offset);
        panel.DragEnded += (_, _) => SaveDragged(_panel.ScreenBounds);
        panel.SizeChanged += (_, _) => KeepPanelInside();
        panel.ContactsEnded += (_, _) => KeepPanelInside();
        panel.Closed += (_, _) => Dispose();
        panel.Presented += (_, _) =>
        {
            _started = true;
            Reflow();
        };
        dock.Labels.PropertyChanged += OnLabelsChanged;
        _panicPill.ApplyLabel(dock.Labels.ReleaseAll);
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
    }

    /// <summary>The bubble.</summary>
    public BubbleWindow Bubble => _bubble;

    /// <summary>The floating «Release all».</summary>
    public PanicPillWindow PanicButton => _panicPill;

    /// <summary>The guide of the Tab view.</summary>
    public DockCoachWindow Coach => _coach;

    /// <summary>The notice surface of the Tab view (PES-014).</summary>
    public DockNoticeWindow Notice => _notice;

    /// <summary>The menu of a shortcut beside the bar (CUA-014); <see langword="null"/> without the layers.</summary>
    public DockMenuWindow? Menu => _menu;

    /// <summary>Quick settings beside the bar (PES-009); <see langword="null"/> without the layers.</summary>
    public DockQuickWindow? Quick => _quick;

    /// <summary>The monitor the panel and the Tab view are on (PES-016).</summary>
    public string MonitorId => CurrentMonitor().Id;

    /// <summary>The monitors as last read.</summary>
    public ImmutableArray<DisplayMonitor> Monitors => _monitors;

    /// <summary>The window beside the bar of <paramref name="flyout"/>.</summary>
    /// <param name="flyout">Pinned, Profiles or Sticky.</param>
    public DockFlyoutWindow Flyout(DockFlyout flyout) => _flyouts[flyout];

    /// <summary>The handle of <paramref name="side"/>, created the first time it is needed.</summary>
    /// <param name="side">The edge.</param>
    public DockHandleWindow Handle(DockSide side)
    {
        if (!_handles.TryGetValue(side, out var handle))
        {
            handle = Track(
                new DockHandleWindow(side, _dock, _registry, _time, _theme, _touch),
                DimSurface.DockHandle
            );
            handle.DragStarted += (_, _) => _handleStart = HandlePercent(side);
            handle.Dragged += (_, drag) => MoveHandle(handle, drag.Offset);
            handle.DragEnded += (_, _) =>
            {
                if (_handleDrag is { } percent)
                {
                    _saveHandle(CurrentMonitor().Id, side, percent);
                }
            };
            _handles[side] = handle;
        }

        return handle;
    }

    /// <summary>The bar of <paramref name="side"/>, created the first time it is needed.</summary>
    /// <param name="side">The edge.</param>
    public DockBarWindow Bar(DockSide side)
    {
        if (!_bars.TryGetValue(side, out var bar))
        {
            bar = Track(
                new DockBarWindow(side, _dock, _registry, _time, _theme, _touch, _holdEnded),
                DimSurface.Dock
            );
            bar.TileTapped += (_, used) => _tileUsed(used.Tile, false);
            bar.HoldReleased += (_, _) => _holdReleased();
            bar.SizeChanged += (_, _) => Reflow();
            _bars[side] = bar;
        }

        return bar;
    }

    /// <summary>Shows the form of <paramref name="layout"/> and places every surface of it.</summary>
    /// <param name="layout">What to show.</param>
    public void Apply(SurfaceLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _panel.VerifyAccess();
        if (_disposed)
        {
            return;
        }

        var previous = _layout;
        _layout = layout;
        if (
            previous?.Dock.HandlePositions != layout.Dock.HandlePositions
            || previous?.Settings?.HandlePositionsByMonitor
                != layout.Settings?.HandlePositionsByMonitor
        )
        {
            _handleDrag = null;
        }

        Reflow();
        if (previous?.Form != layout.Form)
        {
            Awake();
        }
    }

    /// <summary>
    /// The touch keyboard appeared, moved or went away (BUS-002, EC-BUS-01): a panel it covers moves right above it,
    /// passively, and goes back where it was when the keyboard hides. Nothing is saved: it is not the user's position.
    /// </summary>
    /// <param name="occluded">What the keyboard covers, in physical pixels; empty when it is hidden.</param>
    public void ApplyOccluded(PhysicalRect occluded)
    {
        _panel.VerifyAccess();
        if (_disposed || _occluded == occluded)
        {
            return;
        }

        _occluded = occluded;
        AvoidKeyboard();
    }

    /// <summary>Takes new touch filter values for every surface (TAC-002).</summary>
    /// <param name="touch">The values.</param>
    public void ApplyTouch(TouchSettings touch)
    {
        _touch = touch;
        foreach (var surface in _surfaces)
        {
            surface.ApplyTouch(touch);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dimmer.Dispose();
        _dock.Labels.PropertyChanged -= OnLabelsChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        foreach (var surface in _surfaces)
        {
            surface.Close();
        }
    }

    private T Track<T>(T surface, DimSurface? kind)
        where T : TouchSurface
    {
        _dimmer.Add(surface, kind, surface);
        surface.ContentRendered += (_, _) => _panel.ReportFirstFrame();
        _surfaces.Add(surface);
        return surface;
    }

    /// <summary>
    /// Shows the surfaces of the form and hides the others, each in its place. A surface that changes size while it is
    /// placed (the bar, a window beside it) asks for another pass, once, after the current one.
    /// </summary>
    private void Reflow()
    {
        if (_disposed || _layout is null)
        {
            return;
        }

        if (_reflowing)
        {
            if (!_reflowQueued)
            {
                _reflowQueued = true;
                _ = _panel.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    () =>
                    {
                        _reflowQueued = false;
                        Reflow();
                    }
                );
            }

            return;
        }

        _reflowing = true;
        try
        {
            ReflowOnce();
        }
        finally
        {
            _reflowing = false;
        }
    }

    private void ReflowOnce()
    {
        if (_layout is not { } layout)
        {
            return;
        }

        var form = layout.Form;
        _panel.FormVisible = PanelForms.IsPanel(form);
        if (!_started)
        {
            return;
        }

        var monitor = CurrentMonitor();
        var side = layout.Dock.Side;
        var shown = new HashSet<TouchSurface>();
        var taken = new List<PhysicalRect>();
        PhysicalRect? panicAnchor = null;
        PhysicalRect? noticeAnchor = null;

        switch (form)
        {
            case PanelForm.Bubble:
                var diameter = monitor.ToPhysical(
                    Clicalo.Domain.Catalog.PanelSizes.Layout.BubbleDiameterPx
                );
                var (bubbleWidth, bubbleHeight) = _bubble.MeasurePhysical(monitor.Scale);
                var bubbleRect = PanelGeometry.Clamp(
                    new PhysicalRect(
                        _topLeft?.X ?? PanelGeometry.Initial(diameter, diameter, monitor).Left,
                        _topLeft?.Y ?? PanelGeometry.Initial(diameter, diameter, monitor).Top,
                        bubbleWidth,
                        bubbleHeight
                    ),
                    monitor
                );
                Present(_bubble, bubbleRect, shown);
                panicAnchor = bubbleRect;
                break;

            case PanelForm.DockClosed:
                var handle = Handle(side);
                handle.IsLocked = layout.Dock.HandleLocked;
                var handleRect = DockGeometry.Handle(
                    side,
                    HandlePercent(side),
                    monitor,
                    layout.Dock.Gutter
                );
                Present(handle, handleRect, shown);
                panicAnchor = handleRect;
                noticeAnchor = handleRect;
                break;

            case PanelForm.DockOpen:
                var bar = Bar(side);
                bar.Monitor = monitor;
                var (width, height) = bar.MeasurePhysical(monitor.Scale);
                var barRect = DockGeometry.Bar(
                    side,
                    DockGeometry.IsVertical(side) ? height : width,
                    layout.Metrics,
                    monitor,
                    layout.Dock.Gutter
                );
                Present(bar, barRect, shown);
                bar.UpdateLayout();
                PlaceFlyouts(layout, bar, monitor, shown, taken);
                noticeAnchor = barRect;
                break;
        }

        if (layout.Panic && form is PanelForm.Bubble or PanelForm.DockClosed or PanelForm.DockOpen)
        {
            var (pillWidth, pillHeight) = _panicPill.MeasurePhysical(monitor.Scale);
            var pillRect = panicAnchor is { } anchor
                ? DockGeometry.Beside(
                    form == PanelForm.Bubble ? DockSide.Right : side,
                    anchor,
                    pillWidth,
                    pillHeight,
                    monitor.ToPhysical(DockGeometry.BarWindowGapPx),
                    DockAlign.Center,
                    monitor
                )
                : DockGeometry.Panic(side, pillWidth, pillHeight, monitor);
            Present(_panicPill, pillRect, shown);
            taken.Add(pillRect);
        }

        if (layout.ShowsNotice && noticeAnchor is { } beside)
        {
            // PES-014: beside the bar, or the handle when it is closed, clear of whatever is already there.
            var (noticeWidth, noticeHeight) = _notice.MeasurePhysical(monitor.Scale);
            Present(
                _notice,
                DockGeometry.BesideClear(
                    side,
                    beside,
                    noticeWidth,
                    noticeHeight,
                    monitor.ToPhysical(DockGeometry.BarWindowGapPx),
                    DockAlign.End,
                    monitor,
                    taken
                ),
                shown
            );
        }

        foreach (var surface in _surfaces)
        {
            if (!shown.Contains(surface) && surface.IsVisible)
            {
                surface.Conceal();
            }
        }

        _dimmer.UpdatePresence();
        _dimmer.Apply();
    }

    private void PlaceFlyouts(
        SurfaceLayout layout,
        DockBarWindow bar,
        DisplayMonitor monitor,
        HashSet<TouchSurface> shown,
        List<PhysicalRect> taken
    )
    {
        var side = layout.Dock.Side;
        var barRect = bar.ScreenBounds;
        var maxHeight = DockGeometry.MaxSideWindowHeight(monitor) / monitor.Scale;
        if (layout.Flyout != DockFlyout.None && _flyouts.TryGetValue(layout.Flyout, out var flyout))
        {
            flyout.MaxContentHeight = maxHeight;
            var (width, height) = flyout.MeasurePhysical(monitor.Scale);
            var anchor = layout.Flyout switch
            {
                DockFlyout.Pinned => bar.PinnedButtonBounds,
                DockFlyout.Sticky => bar.StickyButtonBounds,
                _ => barRect,
            };
            var byButton =
                layout.Flyout is DockFlyout.Pinned or DockFlyout.Sticky && !anchor.IsEmpty;
            var rect = DockGeometry.Beside(
                side,
                byButton ? anchor : barRect,
                width,
                Math.Min(height, monitor.ToPhysical(maxHeight)),
                monitor.ToPhysical(
                    byButton ? DockGeometry.ButtonWindowGapPx : DockGeometry.BarWindowGapPx
                ),
                byButton ? DockAlign.End : DockAlign.Start,
                monitor
            );
            Present(flyout, rect, shown);
            taken.Add(rect);
        }

        if (layout.ShowsCoach)
        {
            var (width, height) = _coach.MeasurePhysical(monitor.Scale);
            var rect = DockGeometry.Beside(
                side,
                barRect,
                width,
                height,
                monitor.ToPhysical(DockGeometry.BarWindowGapPx),
                DockAlign.Start,
                monitor
            );
            Present(_coach, rect, shown);
            taken.Add(rect);
        }

        if (layout.ShowsMenu && _menu is { } menu)
        {
            // CUA-014, PES-010: the menu of a shortcut beside the bar, or beside «Pinned» when it is open.
            var (width, height) = menu.MeasurePhysical(monitor.Scale);
            var rect = DockGeometry.BesideClear(
                side,
                barRect,
                width,
                height,
                monitor.ToPhysical(DockGeometry.BarWindowGapPx),
                DockAlign.Start,
                monitor,
                taken
            );
            Present(menu, rect, shown);
            taken.Add(rect);
        }

        if (layout.ShowsQuick && _quick is { } quick)
        {
            // PES-009: Quick settings beside the tune button, as «Pinned» is beside its button.
            quick.FitHeight(maxHeight);
            var (width, height) = quick.MeasurePhysical(monitor.Scale);
            var anchor = bar.TuneButtonBounds;
            var byButton = !anchor.IsEmpty;
            var rect = DockGeometry.BesideClear(
                side,
                byButton ? anchor : barRect,
                width,
                Math.Min(height, monitor.ToPhysical(maxHeight)),
                monitor.ToPhysical(
                    byButton ? DockGeometry.ButtonWindowGapPx : DockGeometry.BarWindowGapPx
                ),
                byButton ? DockAlign.End : DockAlign.Start,
                monitor,
                taken
            );
            Present(quick, rect, shown);
            taken.Add(rect);
        }
    }

    private static void Present(
        TouchSurface surface,
        PhysicalRect bounds,
        HashSet<TouchSurface> shown
    )
    {
        surface.PresentAt(bounds);
        _ = shown.Add(surface);
    }

    /// <summary>The panel's monitor: the one it was on if still connected, the primary one otherwise (PAN-006).</summary>
    private DisplayMonitor CurrentMonitor() =>
        PanelGeometry.Find(_monitors, _monitorId) ?? StartMonitor();

    private DisplayMonitor StartMonitor()
    {
        var (width, height) = _panel.MeasurePhysical(PanelGeometry.Primary(_monitors).Scale);
        var placement = PanelGeometry.Place(
            width,
            height,
            _layout?.Positions ?? [],
            _monitorId,
            _monitors
        );
        _monitorId = placement.Monitor.Id;
        _topLeft ??= new PhysicalPoint(placement.Bounds.Left, placement.Bounds.Top);
        return placement.Monitor;
    }

    /// <summary>The panel, each time it appears: where it was, or where it was saved for its monitor (PAN-006).</summary>
    private void PlacePanel()
    {
        if (_occluded.IsEmpty && _beforeKeyboard is { } back)
        {
            // The keyboard went away while the panel was hidden: back where it was (BUS-002).
            _beforeKeyboard = null;
            _topLeft = back;
        }

        var monitor = CurrentMonitor();
        var (width, height) = _panel.MeasurePhysical(monitor.Scale);
        var rect = _topLeft is { } topLeft
            ? PanelGeometry.Clamp(new PhysicalRect(topLeft.X, topLeft.Y, width, height), monitor)
            : PanelGeometry
                .Place(width, height, _layout?.Positions ?? [], monitor.Id, _monitors)
                .Bounds;
        _topLeft = new PhysicalPoint(rect.Left, rect.Top);
        _panel.WorkAreaBottom = monitor.WorkArea.Bottom / monitor.Scale;

        // BUS-002: never under the touch keyboard; where it was is kept for when the keyboard hides.
        var clear = PanelGeometry.Avoid(rect, _occluded, monitor);
        if (clear != rect)
        {
            _beforeKeyboard ??= _topLeft;
            _topLeft = new PhysicalPoint(clear.Left, clear.Top);
            rect = clear;
        }

        if (_panel.ScreenBounds != rect)
        {
            _panel.MovePassive(rect);
        }
    }

    /// <summary>
    /// BUS-002, EC-BUS-01: moves the panel clear of the touch keyboard, or back where it was once the keyboard is gone.
    /// Never under a finger (PAN-009): it waits for the contacts to end.
    /// </summary>
    private void AvoidKeyboard()
    {
        if (_disposed || !_panel.IsVisible || _panel.IsTouching)
        {
            return;
        }

        if (_occluded.IsEmpty)
        {
            if (_beforeKeyboard is { } back)
            {
                _beforeKeyboard = null;
                _topLeft = back;
                PlacePanel();
            }

            return;
        }

        var bounds = _panel.ScreenBounds;
        if (bounds.IsEmpty)
        {
            return;
        }

        var clear = PanelGeometry.Avoid(bounds, _occluded, CurrentMonitor());
        if (clear != bounds)
        {
            _beforeKeyboard ??= new PhysicalPoint(bounds.Left, bounds.Top);
            _topLeft = new PhysicalPoint(clear.Left, clear.Top);
            _panel.MovePassive(clear);
        }
    }

    /// <summary>PAN-006: after a change of size the panel is kept whole inside the work area (never under a finger).</summary>
    private void KeepPanelInside()
    {
        if (_disposed || !_panel.IsVisible || _panel.IsTouching)
        {
            return;
        }

        var bounds = _panel.ScreenBounds;
        if (bounds.IsEmpty)
        {
            return;
        }

        var monitor = CurrentMonitor();
        var clamped = PanelGeometry.Clamp(bounds, monitor);
        if (clamped != bounds)
        {
            _panel.MovePassive(clamped);
            _topLeft = new PhysicalPoint(clamped.Left, clamped.Top);
        }

        AvoidKeyboard();
    }

    private void MoveDragged(Window surface, PhysicalOffset offset)
    {
        if (_dragOrigin.IsEmpty)
        {
            return;
        }

        var moved = _dragOrigin with
        {
            Left = _dragOrigin.Left + offset.Dx,
            Top = _dragOrigin.Top + offset.Dy,
        };
        var monitor = PanelGeometry.MonitorOf(moved, _monitors);
        var rect = PanelGeometry.Clamp(moved, monitor);

        // The panel and the bubble share their place (BUR-001): a pass of the layout during the drag keeps it.
        _monitorId = monitor.Id;
        _topLeft = new PhysicalPoint(rect.Left, rect.Top);
        if (surface is NonActivatingWindow window)
        {
            window.MovePassive(rect);
        }
    }

    /// <summary>The panel or the bubble ended a drag: that is its position on its monitor (PAN-004, PAN-006, BUR-001).</summary>
    private void SaveDragged(PhysicalRect bounds)
    {
        _dragOrigin = PhysicalRect.Empty;
        if (bounds.IsEmpty)
        {
            return;
        }

        var monitor = PanelGeometry.MonitorOf(bounds, _monitors);
        _monitorId = monitor.Id;
        _topLeft = new PhysicalPoint(bounds.Left, bounds.Top);

        // The user chose this place, keyboard or not: there is nowhere to go back to (BUS-002).
        _beforeKeyboard = null;
        _panel.WorkAreaBottom = monitor.WorkArea.Bottom / monitor.Scale;
        if (monitor.Id.Length > 0)
        {
            _savePosition(new MonitorPosition(monitor.Id, bounds.Left, bounds.Top));
        }
    }

    private void MoveHandle(DockHandleWindow handle, PhysicalOffset offset)
    {
        if (_layout is not { } layout)
        {
            return;
        }

        var monitor = CurrentMonitor();
        var percent = DockGeometry.PercentAfterDrag(handle.Side, _handleStart, offset, monitor);
        _handleDrag = percent;
        handle.MovePassive(DockGeometry.Handle(handle.Side, percent, monitor, layout.Dock.Gutter));
    }

    private int HandlePercent(DockSide side)
    {
        if (_handleDrag is { } dragged)
        {
            return dragged;
        }

        // PES-016: the position saved for this monitor and edge; an unknown monitor uses the one per edge.
        if (_layout?.Settings is { } settings)
        {
            return MonitorHandlePositions.PositionFor(settings, CurrentMonitor().Id, side);
        }

        var positions = _layout?.Dock.HandlePositions;
        return positions is null
            ? 50
            : side switch
            {
                DockSide.Left => positions.Left,
                DockSide.Top => positions.Top,
                DockSide.Bottom => positions.Bottom,
                _ => positions.Right,
            };
    }

    private void Awake()
    {
        if (!_disposed)
        {
            _dimmer.Shown();
        }
    }

    private void OnLabelsChanged(object? sender, PropertyChangedEventArgs e) =>
        _panicPill.ApplyLabel(_dock.Labels.ReleaseAll);

    /// <summary>A monitor was connected, removed or changed resolution (PAN-006): read them again and place everything.</summary>
    private void OnDisplayChanged(object? sender, EventArgs e) =>
        _ = _panel.Dispatcher.BeginInvoke(Redisplay);

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            string.Equals(
                e.PropertyName,
                nameof(SystemParameters.WorkArea),
                StringComparison.Ordinal
            )
        )
        {
            _ = _panel.Dispatcher.BeginInvoke(Redisplay);
        }
    }

    private void Redisplay()
    {
        if (_disposed)
        {
            return;
        }

        _monitors = DisplayMonitors.Snapshot();
        if (PanelGeometry.Find(_monitors, _monitorId) is null)
        {
            // The monitor is gone: the primary one, at the position saved for it (PAN-006).
            _monitorId = null;
            _topLeft = null;
        }

        if (_panel.IsVisible)
        {
            PlacePanel();
        }

        Reflow();
    }
}
