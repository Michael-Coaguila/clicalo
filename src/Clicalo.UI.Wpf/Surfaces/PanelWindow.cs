using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.Presentation.Panel.Search;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Surfaces.Panel.Header;
using Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;
using Clicalo.UI.Wpf.Surfaces.Panel.Search;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;
using GestureTarget = Clicalo.Domain.Touch.TouchTarget;
using TouchTargetSize = Clicalo.UI.Wpf.Controls.TouchTarget;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The full panel (blueprint §8.1, docs/04 §1–§11): a <see cref="NonActivatingWindow"/> (CLC0001 applies) that
/// composes, from the top, the header, the panic strip, the administrator notice, the search, the profile suggestion and
/// the body (Always visible row, sticky keys, profile selector and grid, shortcut grid, pager and notice bar), in the
/// order of PAN-007. It shows what its view models say and forwards what happens:
/// <list type="bullet">
/// <item>finger, pen and mouse go through the product's pointer layer (<see cref="PointerInputSource"/> feeding a
/// <see cref="GestureHost"/>, ADR-0006): every visible tile and button is a target; an accepted tap, the start and the
/// end of a hold and a page swipe reach the view models with the contact's device and summary;</item>
/// <item>UI Automation Invoke, Toggle and ExpandCollapse reach the same actions through the controls themselves (EJE-005,
/// S3);</item>
/// <item>the panic strip and the notice bar are live regions (assertive and polite, ACC-001);</item>
/// <item>with the layers of the panel, Quick settings open under the search, the tile menu above the grid and the test
/// mode indicator above the notice bar; every gesture on a tile asks <see cref="PanelLayerModels.Modes"/> first (edit
/// mode, test mode, the long press of the menu), and while the menu is open any other tap only closes it (CUA-014).</item>
/// </list>
/// It never takes the foreground (REG-01): it is shown and moved only passively, and hiding it resets the gestures, so
/// a hold under the finger ends with <see cref="HoldEndReason.Reset"/> and the engine releases it (REG-03). While a
/// finger rests on it, nothing that appears above the grid moves a button under it: the window moves up by as much
/// (PAN-009).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "A WPF window's lifetime ends with Close: OnClosed disposes the pointer layer, the gestures and the timers and detaches the theme."
)]
public sealed class PanelWindow : NonActivatingWindow, IPointerFrameSink, IPointerPresence
{
    /// <summary>The target identifier of «Release all»; tiles and buttons take the ones after it.</summary>
    private const int ReleaseAllTargetId = 0;

    private const double PanicTextPx = 14;
    private const double PanicIconPx = 18;

    private readonly PanelViewModel _viewModel;
    private readonly PanelHeaderViewModel? _headerViewModel;
    private readonly SearchViewModel? _search;
    private readonly SuggestionViewModel? _suggestion;
    private readonly PanelLayerModels? _layers;
    private readonly QuickSettingsSheet? _quickSheet;
    private readonly TimeProvider _time;
    private readonly ThemeService _theme;
    private readonly StackPanel _root;
    private readonly PanelBodyView _body;
    private readonly SearchBar? _searchBar;
    private readonly SuggestionCard? _suggestionCard;
    private readonly Border _panicStrip;
    private readonly TextBlock _panicText;
    private readonly ShortcutTile _releaseAll;
    private readonly Dictionary<int, Target> _targets = [];
    private readonly Dictionary<ShortcutId, int> _tileIds = [];
    private readonly Dictionary<FrameworkElement, int> _elementIds = [];
    private readonly ContactTracker _contacts = new();
    private readonly HashSet<uint> _dragged = [];
    private PanelHeader? _header;
    private SizeMetrics? _headerSize;
    private int _nextTargetId = ReleaseAllTargetId + 1;
    private DimSettings _dim;
    private bool _hovered;
    private bool _touching;
    private bool _formVisible = true;
    private bool _presented;
    private bool _rendered;
    private int? _gridAnchor;
    private DragTracker? _drag;
    private GestureHost? _gestures;
    private PointerInputSource? _pointer;
    private LiveAnnouncer? _panicAnnouncer;
    private bool _placed;
    private bool _closed;
    private bool _measuring;

    /// <summary>Creates the panel on the UI thread of <paramref name="registry"/> (the Surfaces role).</summary>
    /// <param name="viewModel">The body and the panic strip; its layout settings give sizes and width.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer frames and the gesture deadlines.</param>
    /// <param name="theme">
    /// The theme service of the UI thread (TEM-001, CUA-011, TEM-006): colors, fonts, text scale and reduce motion.
    /// </param>
    /// <param name="dim">The opacity and the automatic dimming (GEN-009).</param>
    /// <param name="header">The header (CAB-001); <see langword="null"/> leaves it out.</param>
    /// <param name="search">The search (BUS-001); <see langword="null"/> leaves it out.</param>
    /// <param name="suggestion">The profile suggestion (PER-009); <see langword="null"/> leaves it out.</param>
    /// <param name="layers">
    /// Quick settings, edit mode, the tile menu and test mode (AJR-001, CUA-012, CUA-014, TAC-008); <see langword="null"/>
    /// leaves them out.
    /// </param>
    public PanelWindow(
        PanelViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        DimSettings dim,
        PanelHeaderViewModel? header = null,
        SearchViewModel? search = null,
        SuggestionViewModel? suggestion = null,
        PanelLayerModels? layers = null
    )
        : base(PanelSurfaceIds.Panel, registry)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(theme);
        _viewModel = viewModel;
        _headerViewModel = header;
        _search = search;
        _suggestion = suggestion;
        _layers = layers;
        _time = time;
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

        (_panicStrip, _panicText, _releaseAll) = BuildPanicStrip();
        _body = new PanelBodyView(viewModel);
        _root = new StackPanel { Orientation = Orientation.Vertical };
        _root.Children.Add(_panicStrip);
        _root.Children.Add(_body.AdminNotice);
        if (search is not null)
        {
            _searchBar = new SearchBar(search);
            _root.Children.Add(_searchBar);
            search.PropertyChanged += OnSearchChanged;
        }

        if (layers is not null)
        {
            // AJR-001: the sheet opens under the search bar, at most 62 % of the screen high.
            _quickSheet = new QuickSettingsSheet(layers.QuickSettings);
            _quickSheet.ApplyScreenHeight(SystemParameters.WorkArea.Height);
            _root.Children.Add(_quickSheet);
            _body.AttachLayers(layers);
            layers.EditMode.PropertyChanged += OnLayerChanged;
            layers.Menu.PropertyChanged += OnLayerChanged;
        }

        if (suggestion is not null)
        {
            _suggestionCard = new SuggestionCard(suggestion);
            _root.Children.Add(_suggestionCard);
        }

        _root.Children.Add(_body);
        Content = _root;
        ApplyLayout();

        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Panic.PropertyChanged += OnPanicChanged;
        LayoutUpdated += (_, _) => OnLayoutUpdated();
        LocationChanged += (_, _) => RefreshTargets();
        ApplyPanic();
    }

    /// <summary>Raised once the app asked to show the panel for the first time (<see cref="Present"/>).</summary>
    public event EventHandler? Presented;

    /// <summary>Raised when the last finger or pen leaves the panel: what waited for it may now move (PAN-009).</summary>
    public event EventHandler? ContactsEnded;

    /// <summary>A finger, the pen or the pointer came onto the panel or left it (<see cref="IsPointerInside"/>, GEN-009).</summary>
    public event EventHandler? PresenceChanged;

    /// <summary>The panel appeared on screen: it is awake and dims a while later (GEN-009).</summary>
    public event EventHandler? Shown;

    /// <summary>New opacity and dimming settings arrived (GEN-009, AJR-004): <see cref="DimSettings"/>.</summary>
    public event EventHandler? DimSettingsChanged;

    /// <summary>A drag of the grip or the title passed the threshold (PAN-004).</summary>
    public event EventHandler? DragStarted;

    /// <summary>The panel is dragged: the offset from where the finger went down, in physical pixels (PAN-004).</summary>
    public event EventHandler<SurfaceDragEventArgs>? Dragged;

    /// <summary>The drag of the panel ended: its position is saved for its monitor (PAN-004, PAN-006).</summary>
    public event EventHandler? DragEnded;

    /// <summary>Whether a finger or the pen rests on the panel.</summary>
    public bool IsTouching => _touching;

    /// <summary>Whether a finger, the pen or the pointer is on the panel.</summary>
    public bool IsPointerInside => _hovered || _touching;

    /// <summary>The opacity and dimming settings last applied (GEN-009).</summary>
    public DimSettings DimSettings => _dim;

    /// <summary>
    /// Whether the panel is the form in front (PAN-001): false while it is the bubble or the Tab view. The panel shows
    /// when the session shows it and this is true.
    /// </summary>
    public bool FormVisible
    {
        get => _formVisible;
        set
        {
            VerifyAccess();
            if (_formVisible == value)
            {
                return;
            }

            _formVisible = value;
            ApplyVisibility();
        }
    }

    /// <summary>
    /// Where the panel goes each time it appears (PAN-006): the surface set places it on its monitor; without one,
    /// the first time it goes to the initial position of the primary monitor.
    /// </summary>
    public Action? Placer { get; set; }

    /// <summary>
    /// The bottom edge of the work area of the panel's monitor, in the window's device-independent pixels: the grid
    /// is measured down to it (CUA-001). Without it, the primary monitor's.
    /// </summary>
    public double? WorkAreaBottom { get; set; }

    /// <summary>The panel on screen, in physical pixels; empty before it has a handle.</summary>
    public PhysicalRect ScreenBounds =>
        PresentationSource.FromVisual(this) is null ? PhysicalRect.Empty : PhysicalBounds(this);

    /// <summary>The shortcut tiles on screen, in display order: the page of the grid, then the Always visible row.</summary>
    public IReadOnlyList<ShortcutTile> TileControls =>
        [.. _body.TileControls.Select(static tile => tile.Control)];

    /// <summary>The header, when the panel has one.</summary>
    public PanelHeader? Header => _header;

    /// <summary>Quick settings, when the panel has its layers.</summary>
    public QuickSettingsSheet? QuickSettingsSheet => _quickSheet;

    /// <summary>The body: the rows, the grid, the pager and the notice bar.</summary>
    public PanelBodyView Body => _body;

    /// <summary>The «Release all» button of the panic strip.</summary>
    public ShortcutTile ReleaseAllButton => _releaseAll;

    /// <summary>Whether the panic strip is on screen.</summary>
    public bool IsPanicStripVisible => _panicStrip.Visibility == Visibility.Visible;

    /// <summary>
    /// Shows or hides the panel as the view model says; the first time, it is placed at its initial position in the
    /// work area of the primary monitor (<c>sizes.json</c>: offsets and margin).
    /// </summary>
    public void Present()
    {
        _presented = true;
        ApplyVisibility();
        Presented?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Another surface of the panel drew its first frame while the panel itself is not the form in front (the Tab view
    /// at start): the start of the app counts it as the first frame (NFR-001).
    /// </summary>
    public void ReportFirstFrame()
    {
        if (!_rendered)
        {
            OnContentRendered(EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void OnContentRendered(EventArgs e)
    {
        _rendered = true;
        base.OnContentRendered(e);
    }

    /// <summary>Takes new opacity and dimming settings; the surface set applies them at once (GEN-009, AJR-004).</summary>
    /// <param name="dim">The settings.</param>
    public void ApplyDimSettings(DimSettings dim)
    {
        VerifyAccess();
        _dim = dim;
        DimSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The size the panel asks for at its content, in physical pixels at <paramref name="scale"/>.</summary>
    /// <param name="scale">Physical pixels per logical pixel of the monitor it goes to.</param>
    public (int Width, int Height) MeasurePhysical(double scale)
    {
        if (Content is not UIElement content)
        {
            return (1, 1);
        }

        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var border = BorderThickness;
        return (
            Math.Max(
                1,
                (int)Math.Ceiling((content.DesiredSize.Width + border.Left + border.Right) * scale)
            ),
            Math.Max(
                1,
                (int)Math.Ceiling((content.DesiredSize.Height + border.Top + border.Bottom) * scale)
            )
        );
    }

    /// <inheritdoc />
    public void OnFrame(in PointerFrame frame)
    {
        foreach (var sample in frame.Samples)
        {
            _contacts.Observe(sample);
            TrackDrag(sample);
            if (_quickSheet?.Track(sample, _drag?.ThresholdPx ?? 0) == true)
            {
                // AJR-001, AJR-002: a finger that slid the opacity or scrolled the sheet is not a tap.
                _ = _dragged.Add(sample.PointerId);
            }
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
                if (sample.Phase is PointerPhase.Up or PointerPhase.Cancel)
                {
                    _dragged.Remove(sample.PointerId);
                }
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

        var before = IsPointerInside;
        _hovered = inside;
        if (before != IsPointerInside)
        {
            PresenceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void OnSurfaceInitialized()
    {
        base.OnSurfaceInitialized();
        _panicAnnouncer = new LiveAnnouncer(_panicText);
        _drag = new DragTracker(_viewModel.Touch, DpiScale());
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
        _drag?.Configure(_viewModel.Touch, newDpi.DpiScaleX);
        RefreshTargets();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Panic.PropertyChanged -= OnPanicChanged;
        if (_search is not null)
        {
            _search.PropertyChanged -= OnSearchChanged;
        }

        if (_layers is not null)
        {
            _layers.EditMode.PropertyChanged -= OnLayerChanged;
            _layers.Menu.PropertyChanged -= OnLayerChanged;
        }

        _body.Detach();
        _quickSheet?.Detach();
        _suggestionCard?.Detach();
        _pointer?.Detach();
        _pointer?.Dispose();

        // On the UI thread: a hold that is still active ends with HoldEndReason.Reset and the engine releases it.
        _gestures?.Dispose();
        _theme.Detach(this);
        base.OnClosed(e);
    }

    private (Border Strip, TextBlock Text, ShortcutTile ReleaseAll) BuildPanicStrip()
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
        };
        text.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(PanicTextPx));
        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.OnDanger)
        );
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Assertive);
        var icon = new SymbolIcon
        {
            Symbol = "warning",
            Size = PanicIconPx,
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.OnDanger)
        );
        var releaseAll = new ShortcutTile
        {
            Template = PanicButtonTemplate.Default,
            Pattern = ShortcutTilePattern.Invoke,
            MinWidth = TouchTargetSize.MinimumSize,
            MinHeight = TouchTargetSize.MinimumSize,
            Padding = new Thickness(12, 0, 12, 0),
            Focusable = false,
            IsTabStop = false,
        };
        releaseAll.SetResourceReference(FontSizeProperty, ThemeKeys.TextSize(PanicTextPx));
        releaseAll.Invoked += (_, _) => _viewModel.Panic.ReleaseAll();
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(releaseAll, Dock.Right);
        row.Children.Add(icon);
        row.Children.Add(releaseAll);
        row.Children.Add(text);
        var strip = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(Radii.Control),
            Padding = new Thickness(10, 4, 4, 4),
            Margin = new Thickness(12, 0, 12, 10),
            Visibility = Visibility.Collapsed,
        };
        strip.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Danger));
        return (strip, text, releaseAll);
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
                _drag?.Configure(_viewModel.Touch, DpiScale());
                break;
            case nameof(PanelViewModel.Layout):
                ApplyLayout();
                break;
        }
    }

    private void OnSearchChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (
            string.Equals(
                change.PropertyName,
                nameof(SearchViewModel.IsSearching),
                StringComparison.Ordinal
            )
        )
        {
            ApplyCompactRow();
        }
    }

    private void OnPanicChanged(object? sender, PropertyChangedEventArgs change) => ApplyPanic();

    /// <summary>Edit mode changes what a tile does (CUA-012) and the menu what a tap does (CUA-014).</summary>
    private void OnLayerChanged(object? sender, PropertyChangedEventArgs change) =>
        RefreshTargets();

    /// <summary>
    /// Width from the layout (PAN-002) and, in the Full and Compact views, the header with the header buttons of the
    /// size (<c>sizes.json</c>); the bottom row of the Compact view (VCO-002). The Tab view has its own surfaces.
    /// </summary>
    private void ApplyLayout()
    {
        var layout = _viewModel.Layout;
        _root.Width = GridMetrics.PanelWidth(layout);
        ApplyCompactRow();
        if (_headerViewModel is null || ReferenceEquals(_headerSize, layout.Metrics))
        {
            return;
        }

        if (_header is not null)
        {
            _root.Children.Remove(_header);
        }

        _headerSize = layout.Metrics;
        _header = new PanelHeader(_headerViewModel, layout.Metrics);
        _root.Children.Insert(0, _header);
    }

    /// <summary>VCO-002: the bottom row of the Compact view, hidden while the search has text.</summary>
    private void ApplyCompactRow() =>
        _body.ApplyCompactRow(
            CompactRowRules.Visible(_viewModel.Layout, _search?.IsSearching == true)
        );

    private void ApplyVisibility()
    {
        if (_closed || !_presented)
        {
            return;
        }

        if (_viewModel.IsVisible && _formVisible)
        {
            if (Placer is { } placer)
            {
                placer();
            }
            else if (!_placed)
            {
                PlaceInitially();
            }

            _placed = true;
            ShowPassive();
            RefreshTargets();

            // It appears awake and dims a while later unless the finger or the pointer is on it (GEN-009).
            Shown?.Invoke(this, EventArgs.Empty);
            return;
        }

        // A hidden surface receives no pointer-up: end its holds now (REG-03) and forget its contacts.
        _gestures?.Reset();
        _contacts.Clear();
        _drag?.Reset();
        _dragged.Clear();
        SetTouching(false);
        if (_hovered)
        {
            _hovered = false;
            PresenceChanged?.Invoke(this, EventArgs.Empty);
        }

        HidePassive();
    }

    /// <summary>Follows whether a finger or the pen is on the panel; lifting the last one counts as leaving it.</summary>
    private void TrackTouching() => SetTouching(_contacts.Count > 0);

    private void SetTouching(bool touching)
    {
        if (touching == _touching)
        {
            return;
        }

        var before = IsPointerInside;
        _touching = touching;
        _gridAnchor = touching ? GridTop() : null;
        if (before != IsPointerInside)
        {
            PresenceChanged?.Invoke(this, EventArgs.Empty);
        }

        if (!touching)
        {
            ContactsEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// PAN-004: a contact that went down on the grip or the title and moved past max(6, cancelMovePx) drags the panel;
    /// it is no longer a tap. The surface set moves the panel and saves where it ends.
    /// </summary>
    private void TrackDrag(in PointerSample sample)
    {
        if (_drag is not { } drag || _header is not { } header)
        {
            return;
        }

        switch (sample.Phase)
        {
            case PointerPhase.Down when !drag.IsTracking:
                foreach (var zone in header.DragZones)
                {
                    if (PhysicalBounds(zone).Contains(sample.Position))
                    {
                        _ = drag.Down(sample.PointerId, sample.Position);
                        break;
                    }
                }

                break;

            case PointerPhase.Move:
                var wasDragging = drag.IsDragging;
                if (drag.Move(sample.PointerId, sample.Position) is { } offset)
                {
                    if (!wasDragging)
                    {
                        DragStarted?.Invoke(this, EventArgs.Empty);
                    }

                    Dragged?.Invoke(this, new SurfaceDragEventArgs(offset));
                }

                break;

            case PointerPhase.Up
            or PointerPhase.Cancel:
                if (
                    sample.Phase == PointerPhase.Up
                    && drag.Move(sample.PointerId, sample.Position) is { } last
                )
                {
                    Dragged?.Invoke(this, new SurfaceDragEventArgs(last));
                }

                if (drag.Up(sample.PointerId) == DragEnd.Dragged)
                {
                    _ = _dragged.Add(sample.PointerId);
                    DragEnded?.Invoke(this, EventArgs.Empty);
                }

                break;
        }
    }

    /// <summary>
    /// After every layout pass: PAN-009 first (while a finger rests on the panel the grid keeps its place on screen),
    /// then the touch targets and the space measured for the grid (CUA-001, CUA-003), which waits for the fingers.
    /// </summary>
    private void OnLayoutUpdated()
    {
        if (_closed || _measuring)
        {
            return;
        }

        _measuring = true;
        try
        {
            KeepGridUnderFinger();
            RefreshTargets();
            if (!_touching && IsVisible)
            {
                _ = _body.MeasureGridSpace(
                    this,
                    WorkAreaBottom ?? SystemParameters.WorkArea.Bottom
                );
                FitQuickSheet();
            }
        }
        finally
        {
            _measuring = false;
        }
    }

    /// <summary>
    /// AJR-001: Quick settings never push the panel off the screen. The sheet gets the height left down to the bottom
    /// of the work area with the grid on one row; what does not fit scrolls inside the sheet. The grid then measures
    /// the rows that fit under it (CUA-001), so the two never chase each other.
    /// </summary>
    private void FitQuickSheet()
    {
        if (
            _quickSheet is not { IsVisible: true } sheet
            || PresentationSource.FromVisual(sheet) is null
        )
        {
            return;
        }

        var top = sheet.TranslatePoint(new Point(0, 0), this).Y;
        var below = ActualHeight - top - sheet.ActualHeight;
        var room =
            (WorkAreaBottom ?? SystemParameters.WorkArea.Bottom)
            - PanelSizes.Layout.PanelBottomMarginPx
            - Top
            - top
            - below
            + _body.GridSlack;
        sheet.FitHeight(room);
    }

    /// <summary>
    /// PAN-009: something appeared or went away above the grid while a finger rests on the panel (the panic strip, the
    /// administrator notice, the suggestion): the window moves by as much, so no button moves under the finger.
    /// </summary>
    private void KeepGridUnderFinger()
    {
        if (
            !_touching
            || _gridAnchor is not { } anchor
            || GridTop() is not { } top
            || top == anchor
        )
        {
            return;
        }

        var scale = DpiScale();
        var origin = PointToScreen(new Point(0, 0));
        var bounds = new PhysicalRect(
            (int)Math.Round(origin.X),
            (int)Math.Round(origin.Y) + (anchor - top),
            (int)Math.Ceiling(ActualWidth * scale),
            (int)Math.Ceiling(ActualHeight * scale)
        );
        if (!bounds.IsEmpty)
        {
            MovePassive(bounds);
        }
    }

    /// <summary>The top of the grid area on screen, in physical pixels; null before the panel has a handle.</summary>
    private int? GridTop() =>
        _body.GridArea.IsVisible && PresentationSource.FromVisual(_body.GridArea) is not null
            ? (int)Math.Round(_body.GridArea.PointToScreen(new Point(0, 0)).Y)
            : null;

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

        // Nothing dims while something is held (SEG-002): the composition reports the panic to the interaction store.
    }

    /// <summary>Every tappable element on screen besides the tiles: header, body, search and suggestion.</summary>
    private IEnumerable<PanelTapTarget> TapTargets()
    {
        if (_header is not null)
        {
            foreach (var target in _header.TapTargets)
            {
                yield return target;
            }
        }

        if (_searchBar is not null)
        {
            yield return new PanelTapTarget(
                _searchBar.Field,
                () => _ = _searchBar.FieldTapped(SearchTrigger.Touch)
            );
            yield return new PanelTapTarget(
                _searchBar.DictateButton,
                () => _ = _searchBar.DictateTapped(SearchTrigger.Touch)
            );
        }

        if (_quickSheet is not null)
        {
            foreach (var target in _quickSheet.TapTargets)
            {
                yield return target;
            }
        }

        if (_suggestionCard is not null && _suggestion is not null)
        {
            yield return new PanelTapTarget(_suggestionCard.CreateButton, _suggestion.Create);
            yield return new PanelTapTarget(_suggestionCard.NotNowButton, _suggestion.NotNow);
        }

        foreach (var target in _body.TapTargets)
        {
            yield return target;
        }
    }

    private void RefreshTargets()
    {
        if (_gestures is not { } gestures || PresentationSource.FromVisual(this) is null)
        {
            return;
        }

        _targets.Clear();
        var targets = ImmutableArray.CreateBuilder<GestureTarget>();

        // CUA-014: while the tile menu is open, its rows act and any other tap only closes it.
        Action? closeMenu = _layers is { Menu.IsOpen: true } open ? open.Menu.Close : null;
        foreach (var tile in _body.TileControls)
        {
            Add(
                tile.Control,
                TileIdOf(tile.ViewModel.Id),
                closeMenu is not null ? TouchTargetKind.Tap
                    : _layers is { } layers ? layers.Modes.KindOf(tile.ViewModel)
                    : tile.ViewModel.Behavior == TileBehavior.Hold ? TouchTargetKind.Hold
                    : TouchTargetKind.Tap,
                closeMenu is null ? new Target(tile.ViewModel, null) : new Target(null, closeMenu)
            );
        }

        foreach (var target in TapTargets())
        {
            Add(
                target.Element,
                ElementIdOf(target.Element),
                TouchTargetKind.Tap,
                new Target(null, closeMenu ?? target.Tap)
            );
        }

        if (closeMenu is not null && _body.TileMenu is { } menu)
        {
            foreach (var target in menu.TapTargets)
            {
                Add(
                    target.Element,
                    ElementIdOf(target.Element),
                    TouchTargetKind.Tap,
                    new Target(null, target.Tap)
                );
            }
        }

        if (_panicStrip.Visibility == Visibility.Visible)
        {
            Add(
                _releaseAll,
                ReleaseAllTargetId,
                TouchTargetKind.Tap,
                new Target(null, _viewModel.Panic.ReleaseAll)
            );
        }

        gestures.Recognizer.SetTargets(targets.ToImmutable());

        // REG-02: every target answers on at least 44 × 44 with an invisible touch margin around its drawing.
        void Add(FrameworkElement element, int id, TouchTargetKind kind, Target target)
        {
            var bounds = TouchBounds.Of(element, inflate: true);
            if (bounds.IsEmpty)
            {
                return;
            }

            targets.Add(new GestureTarget(new TouchTargetId(id), bounds, kind));
            _targets[id] = target;
        }
    }

    /// <summary>
    /// The target identifier of a tile, stable while the panel lives: a contact that went down before the tiles were
    /// rebuilt or reordered keeps its target in the recognizer (PAN-009) and still lifts on the shortcut it touched,
    /// and the filter memory of the recognizer (TAC-002) stays with its tile.
    /// </summary>
    private int TileIdOf(ShortcutId shortcut)
    {
        if (!_tileIds.TryGetValue(shortcut, out var id))
        {
            id = _nextTargetId++;
            _tileIds[shortcut] = id;
        }

        return id;
    }

    /// <summary>The target identifier of a button, stable while its element lives.</summary>
    private int ElementIdOf(FrameworkElement element)
    {
        if (!_elementIds.TryGetValue(element, out var id))
        {
            id = _nextTargetId++;
            _elementIds[element] = id;
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
            case GestureKind.Tap
                when !_dragged.Contains(gesture.PointerId) && TargetOf(gesture) is { } target:
                if (target.Tile is { } tapped)
                {
                    // EJE-001: edit mode and test mode take the tap before the engine.
                    if (_layers?.Modes.Tapped(tapped) == true)
                    {
                        break;
                    }

                    tapped.Tapped(
                        gesture.PointerId,
                        _contacts.DeviceOf(gesture.PointerId),
                        _contacts.Summarize(gesture.PointerId, gesture.Timestamp, DpiScale()),
                        gesture.Timestamp
                    );
                }
                else
                {
                    target.Tap?.Invoke();
                }

                break;

            case GestureKind.HoldStart when TargetOf(gesture)?.Tile is { } held:
                if (_layers?.Modes.HoldStarted(held) == true)
                {
                    break;
                }

                held.HoldStarted(
                    gesture.PointerId,
                    _contacts.DeviceOf(gesture.PointerId),
                    gesture.Timestamp
                );
                break;

            case GestureKind.LongPress
                when _layers is { } layers && TargetOf(gesture)?.Tile is { } pressed:
                // CUA-014: 600 ms without moving opens the tile menu instead of running the tile.
                _ = layers.Modes.LongPressed(pressed, layers.InFrequents());
                break;

            case GestureKind.Ignored
                when _layers is { } layers && TargetOf(gesture)?.Tile is { } ignored:
                // TAC-008: test mode marks an ignored touch with its reason.
                layers.Modes.Ignored(ignored, gesture.Ignored);
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

            case GestureKind.Swipe
                when gesture.Swipe != SwipeDirection.None && !_dragged.Contains(gesture.PointerId):
                // CUA-005: more than 60 px sideways turns the page; the recognizer then ignores taps for a moment.
                _body.Swiped(towardLeft: gesture.Swipe == SwipeDirection.Left);
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

    /// <summary>What a touch target does: a tile's gestures, or one action for a button.</summary>
    private sealed record Target(TileViewModel? Tile, Action? Tap);
}
