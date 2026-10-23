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
using Clicalo.UI.Wpf.Surfaces.Panel.Search;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;
using GestureTarget = Clicalo.Domain.Touch.TouchTarget;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The full panel (blueprint §8.1, docs/04 §1–§11) in direction C (D12): a <see cref="NonActivatingWindow"/> (CLC0001
/// applies) with the size of its design: the header, the body (the Always visible row, the sticky keys, the row «Qué
/// ver», the grid and its pager, in the order of PAN-007) and the status row at its foot. Every temporary state is a
/// layer over those rows, never a block that pushes them (<see cref="PanelLayerHost"/>): Quick settings and the search
/// over the body, the profile grid over the grid, the administrator notice, the profile suggestion and the tile menu at
/// the foot of the body, the test mode band over the pager row and the panic strip with «Release all» over the status
/// row. So the panel never changes its size for them, nothing moves under the finger and a row a layer covers is not on
/// screen for the finger nor for UI Automation. It shows what its view models say and forwards what happens:
/// <list type="bullet">
/// <item>finger, pen and mouse go through the product's pointer layer (<see cref="PointerInputSource"/> feeding a
/// <see cref="GestureHost"/>, ADR-0006): every visible tile and button is a target; an accepted tap, the start and the
/// end of a hold and a page swipe reach the view models with the contact's device and summary;</item>
/// <item>UI Automation Invoke, Toggle and ExpandCollapse reach the same actions through the controls themselves (EJE-005,
/// S3);</item>
/// <item>the panic strip and the status row are live regions (assertive and polite, ACC-001);</item>
/// <item>every gesture on a tile asks <see cref="PanelLayerModels.Modes"/> first (edit mode, test mode, the long press of
/// the menu), and while the menu is open any other tap only closes it (CUA-014).</item>
/// </list>
/// It never takes the foreground (REG-01): it is shown and moved only passively, and hiding it resets the gestures, so
/// a hold under the finger ends with <see cref="HoldEndReason.Reset"/> and the engine releases it (REG-03). While a
/// finger rests on it, nothing that would move the grid moves a button under it: the window moves up by as much
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

    private readonly PanelViewModel _viewModel;
    private readonly PanelHeaderViewModel? _headerViewModel;
    private readonly SearchViewModel? _search;
    private readonly SuggestionViewModel? _suggestion;
    private readonly PanelLayerModels? _layers;
    private readonly TimeProvider _time;
    private readonly ThemeService _theme;
    private readonly StackPanel _root;
    private readonly PanelLayerHost _host;
    private readonly Border _headerSlot;
    private readonly PanelBodyView _body;
    private readonly Border _bottomLayer;
    private bool _footTakesBody;
    private bool _footCovered;
    private readonly SearchBar? _searchBar;
    private readonly SuggestionCard? _suggestionCard;
    private readonly Border _panicStrip;
    private readonly StyledText _panicText;
    private readonly ShortcutTile _releaseAll;
    private readonly Dictionary<int, Target> _targets = [];
    private readonly Dictionary<ShortcutId, int> _tileIds = [];
    private readonly Dictionary<FrameworkElement, int> _elementIds = [];
    private readonly ContactTracker _contacts = new();
    private readonly HashSet<uint> _dragged = [];
    private PanelHeader? _header;
    private SizeMetrics? _headerSize;
    private bool _headerNamed;
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
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);

        (_panicStrip, _panicText, _releaseAll) = BuildPanicStrip();
        _body = new PanelBodyView(viewModel, header)
        {
            Margin = new Thickness(Space.S3, Space.S3, Space.S3, 0),
        };
        _headerSlot = new Border
        {
            Margin = new Thickness(Space.S3, Space.S3, Space.S3, 0),
            Visibility = header is null ? Visibility.Collapsed : Visibility.Visible,
        };
        _body.Notices.Margin = new Thickness(0, Space.S2, 0, 0);
        _root = new StackPanel { Orientation = Orientation.Vertical };
        _root.Children.Add(_headerSlot);
        _root.Children.Add(_body);
        _root.Children.Add(_body.Notices);

        // The layers, in the order of PAN-007 from the top; the last ones are drawn above the first ones.
        _host = new PanelLayerHost(_root);
        if (search is not null)
        {
            _searchBar = new SearchBar(search);
            _host.Add(_searchBar, SearchRegion);
            search.PropertyChanged += OnSearchChanged;
        }

        if (layers is not null)
        {
            // Quick settings open beside the panel, in the tall window of the Tab view (AJR-001, D21): inside the
            // panel of fixed size they had to be scrolled in S, M and L.
            _body.AttachLayers(layers);
            layers.EditMode.PropertyChanged += OnLayerChanged;
            layers.Menu.PropertyChanged += OnLayerChanged;
            layers.QuickSettings.PropertyChanged += OnLayerChanged;
            layers.TestMode.PropertyChanged += OnLayerChanged;
        }

        _host.Add(_body.Picker, PickerRegion);
        viewModel.Picker.PropertyChanged += OnPickerChanged;

        // The notices that wait for an answer go at the foot of the body, one above the other, over the tiles; the layer
        // starts in the gap above the first row it covers, never across a row, and the cards stay at its foot.
        var notices = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        notices.Children.Add(_body.AdminNotice);
        if (suggestion is not null)
        {
            _suggestionCard = new SuggestionCard(suggestion);
            notices.Children.Add(_suggestionCard);
        }

        if (_body.TileMenu is { } menu)
        {
            notices.Children.Add(menu);
        }

        var visibility = DependencyPropertyDescriptor.FromProperty(
            VisibilityProperty,
            typeof(UIElement)
        );
        foreach (FrameworkElement notice in notices.Children)
        {
            notice.Margin = new Thickness(0, 0, 0, Space.S3);
            visibility.AddValueChanged(notice, OnNoticeShown);
        }

        _bottomLayer = PanelChrome.Layer(notices);
        _bottomLayer.Padding = new Thickness(Space.S3, Space.S3, Space.S3, 0);
        _host.Add(_bottomLayer, FootLayerRegion, bottom: true, snapTop: SnapToRow);
        if (_body.TestModeIndicator is { } testMode)
        {
            _host.Add(testMode, PagerRegion);
        }

        _host.Add(_panicStrip, StatusRegion);
        Content = _host;
        ApplyLayout();
        ApplyBottomLayer();

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
            if (_body.Picker.Track(sample, _drag?.ThresholdPx ?? 0))
            {
                // TAC-004: a finger that scrolled the profile grid chooses no profile.
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
            _layers.QuickSettings.PropertyChanged -= OnLayerChanged;
            _layers.TestMode.PropertyChanged -= OnLayerChanged;
        }

        _viewModel.Picker.PropertyChanged -= OnPickerChanged;
        _header?.Detach();
        _body.Detach();
        _suggestionCard?.Detach();
        _pointer?.Detach();
        _pointer?.Dispose();

        // On the UI thread: a hold that is still active ends with HoldEndReason.Reset and the engine releases it.
        _gestures?.Dispose();
        _theme.Detach(this);
        base.OnClosed(e);
    }

    /// <summary>
    /// The panic strip (SEG-002) of direction C: a layer over the status row, so nothing moves when it appears, in the
    /// danger fill down to the rounded foot of the panel, with ⚠ and what is held in bold <c>onDanger</c> (one line with
    /// «…», an assertive live region) and «Release all», the danger button of 44.
    /// </summary>
    private (Border Strip, StyledText Text, ShortcutTile ReleaseAll) BuildPanicStrip()
    {
        // Two lines fit the 44 of «Release all»: what is held reads whole in S («Pulsado: Copiar, Mayús fija»).
        var text = new StyledText(string.Empty, TextStyleToken.Label, ColorToken.OnDanger)
        {
            MaxLines = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Assertive);
        var icon = PanelChrome.Icon("warning", IconBox.Small, ColorToken.OnDanger);
        icon.Margin = new Thickness(0, 0, Space.S2, 0);
        var releaseAll = new ShortcutTile
        {
            Template = PanicButtonTemplate.Default,
            Pattern = ShortcutTilePattern.Invoke,
            Height = ControlHeight.Small,
            Padding = new Thickness(Space.S4, 0, Space.S4, 0),
            Margin = new Thickness(Space.S2, 0, 0, 0),
            Focusable = false,
            IsTabStop = false,
        };
        releaseAll.Invoked += (_, _) => _viewModel.Panic.ReleaseAll();
        var row = new DockPanel { LastChildFill = true, Height = ControlHeight.Small };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(releaseAll, Dock.Right);
        row.Children.Add(icon);
        row.Children.Add(releaseAll);
        row.Children.Add(text);
        var strip = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(0, 0, Radii.Large, Radii.Large),
            // The strip covers half the gap above the status row, the row and the foot of the panel: «Release all» sits
            // S1 from the top and the bottom, centred in the red, and S3 from the right like the rows above.
            Padding = new Thickness(Space.S3, Space.S1, Space.S3, Space.S1),
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
        switch (change.PropertyName)
        {
            case nameof(SearchViewModel.IsSearching):
                ApplyCompactRow();
                break;
            case nameof(SearchViewModel.IsOpen):
                ApplyCover();
                break;
        }
    }

    private void OnPickerChanged(object? sender, PropertyChangedEventArgs change) => ApplyCover();

    private void OnNoticeShown(object? sender, EventArgs e) => ApplyBottomLayer();

    private void OnPanicChanged(object? sender, PropertyChangedEventArgs change) => ApplyPanic();

    /// <summary>
    /// Edit mode changes what a tile does (CUA-012), the menu what a tap does (CUA-014) and Quick settings what is on
    /// screen under them.
    /// </summary>
    private void OnLayerChanged(object? sender, PropertyChangedEventArgs change)
    {
        ApplyCover();
        RefreshTargets();
    }

    /// <summary>
    /// Width from the layout (PAN-002) and, in the Full and Compact views, the header of the size: in S it keeps the
    /// grip and the icon of the profile and leaves its name to the row «Qué ver». The Tab view has its own surfaces.
    /// </summary>
    private void ApplyLayout()
    {
        var layout = _viewModel.Layout;
        _root.Width = GridMetrics.PanelWidth(layout);

        ApplyFoot();
        ApplyCompactRow();
        var named = layout.Metrics.Size != Clicalo.Domain.Catalog.PanelSize.S;
        if (
            _headerViewModel is null
            || (ReferenceEquals(_headerSize, layout.Metrics) && _headerNamed == named)
        )
        {
            return;
        }

        _header?.Detach();
        _headerSize = layout.Metrics;
        _headerNamed = named;
        _header = new PanelHeader(_headerViewModel, named);
        _headerSlot.Child = _header;
    }

    /// <summary>
    /// The status row at the foot. D21: Compact has none, its notices show for a moment in a window beside the panel;
    /// while something is held the row comes back under the last row for the panic strip, so «Release all» never covers
    /// «Qué ver» or the pages (a sticky Ctrl must still let the person change profile), and only the bottom edge moves.
    /// </summary>
    private void ApplyFoot()
    {
        var foot = !_viewModel.Layout.Compact || _viewModel.Panic.IsVisible;
        _body.Notices.Visibility = foot ? Visibility.Visible : Visibility.Collapsed;
        _body.Margin = new Thickness(Space.S3, Space.S3, Space.S3, foot ? 0 : Space.S3);
    }

    /// <summary>VCO-002: the row «Qué ver» goes to the foot in the Compact view, hidden while the search has text.</summary>
    private void ApplyCompactRow() =>
        _body.ApplyCompactRow(
            CompactRowRules.Visible(_viewModel.Layout, _search?.IsSearching == true)
        );

    /// <summary>
    /// What the open layers cover stops being on screen, so neither the finger nor UI Automation reaches a tile under a
    /// layer: the search field the row «Qué ver», the profile grid the grid and its pager, and the test mode band the
    /// pager. Quick settings open beside the panel and cover nothing (D21).
    /// </summary>
    private void ApplyCover()
    {
        _footCovered = FootCovers();
        _body.Cover(
            chooser: _search?.IsOpen == true,
            grid: _viewModel.Picker.IsVisible,
            pager: _layers?.TestMode.IsOn == true,
            foot: _footCovered
        );
    }

    /// <summary>The layer at the foot of the body shows while one of its notices does.</summary>
    private void ApplyBottomLayer()
    {
        var shown =
            _bottomLayer.Child is System.Windows.Controls.Panel notices
            && notices
                .Children.OfType<UIElement>()
                .Any(static n => n.Visibility == Visibility.Visible);
        _bottomLayer.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Where the body starts under the header, in the host: the layers over the body start there.</summary>
    private double BodyTop() =>
        _headerSlot.Visibility == Visibility.Visible && _host.IsAncestorOf(_headerSlot)
            ? _headerSlot.TranslatePoint(default, _host).Y + _headerSlot.ActualHeight + Space.S2
            : 0;

    /// <summary>The top of <paramref name="element"/> in the host.</summary>
    private double TopOf(FrameworkElement element) =>
        _host.IsAncestorOf(element) ? element.TranslatePoint(default, _host).Y : 0;

    private Rect Span(double top, double bottom) =>
        new(0, top, _host.ActualWidth, Math.Max(0, bottom - top));

    /// <summary>The body, from under the header to the status row or, in Compact, the foot (the notices at its foot).</summary>
    private Rect BodyRegion() => Span(BodyTop(), FootTop());

    /// <summary>
    /// The region of the layer at the foot of the body (the notices that wait for an answer, the tile menu): the body,
    /// and in Compact the body above «Qué ver», so the cards of a notice never bury the row of the profiles and the
    /// pages. A card taller than that covers the row too rather than being cut (CUA-011, D21 review: the administrator
    /// notice in S Compact without the sticky keys row). The tile menu keeps the whole body: any tap outside it only
    /// closes it (CUA-014).
    /// </summary>
    private Rect FootLayerRegion()
    {
        var body = BodyRegion();
        if (
            !_viewModel.Layout.Compact
            || _body.Selector.Visibility == Visibility.Collapsed
            || _body.TileMenu?.Visibility == Visibility.Visible
        )
        {
            _footTakesBody = false;
            return body;
        }

        var above = Span(BodyTop(), TopOf(_body.Selector) - Space.S2);
        _bottomLayer.Measure(new Size(above.Width, double.PositiveInfinity));
        _footTakesBody = _bottomLayer.DesiredSize.Height > above.Height;
        return _footTakesBody ? body : above;
    }

    /// <summary>
    /// Whether the layer at the foot of the body shows over the whole Compact body (<see cref="FootLayerRegion"/>): the
    /// rows under it are hidden then, as under any layer, so neither the finger nor UI Automation reaches them.
    /// </summary>
    private bool FootCovers() =>
        _footTakesBody
        && _bottomLayer.Visibility == Visibility.Visible
        && _viewModel.Layout.Compact;

    /// <summary>
    /// <paramref name="top"/>, or the middle of the gap above the row of the body it would cut (a row of tiles, the row
    /// of Always visible, the sticky keys, «Qué ver», the pager), so a layer never slices a row and leaves half tiles on
    /// screen that look tappable.
    /// </summary>
    private double SnapToRow(double top)
    {
        foreach (var (rowTop, rowBottom) in RowBands())
        {
            if (rowTop < top && top < rowBottom)
            {
                return rowTop - Space.S1;
            }
        }

        return top;
    }

    /// <summary>The top and bottom of every row of the body on screen, in the host.</summary>
    private IEnumerable<(double Top, double Bottom)> RowBands()
    {
        foreach (var tile in _body.TileControls)
        {
            yield return Band(tile.Control);
        }

        FrameworkElement[] rows =
        [
            _body.Strip,
            _body.Sticky,
            _body.Selector,
            _body.EmptyState,
            _body.NoResults,
            _body.Pager,
        ];
        foreach (var row in rows)
        {
            yield return Band(row);
        }

        (double, double) Band(FrameworkElement element) =>
            element.IsVisible && _host.IsAncestorOf(element)
                ? (TopOf(element), TopOf(element) + element.ActualHeight)
                : (0, 0);
    }

    /// <summary>Where the body ends: the top of the status row, or the bottom of the panel less its margin in Compact.</summary>
    private double FootTop() =>
        _body.Notices.Visibility == Visibility.Visible
            ? TopOf(_body.Notices)
            : Math.Max(0, _host.ActualHeight - Space.S3);

    /// <summary>
    /// The search field (BUS-001): in the place of the row «Qué ver», right above the results in Full and at the foot in
    /// Compact, as high as that row, so the panel looks the same with the field open and empty (BUS-005). Without that row
    /// (<c>showTabsRow</c> off) it takes the same place above the grid.
    /// </summary>
    private Rect SearchRegion()
    {
        var chooser = _body.Selector;
        var top =
            chooser.Visibility != Visibility.Collapsed
                ? TopOf(chooser)
                : Math.Max(BodyTop(), TopOf(_body.GridArea) - Space.S3 - ControlHeight.Medium);
        return new Rect(
            Space.S3,
            top,
            Math.Max(0, _host.ActualWidth - (2 * Space.S3)),
            ControlHeight.Medium
        );
    }

    /// <summary>The grid and its pager (the profile grid): above the status row in Full, above «Qué ver» in Compact.</summary>
    private Rect PickerRegion()
    {
        var bottom = _viewModel.Layout.Compact ? TopOf(_body.Selector) - Space.S2 : FootTop();
        return Span(TopOf(_body.GridArea) - Space.S1, bottom);
    }

    /// <summary>The pager row, inside the margins of the body (the test mode band).</summary>
    private Rect PagerRegion()
    {
        var top = TopOf(_body.Pager);
        return new Rect(
            Space.S3,
            top,
            Math.Max(0, _host.ActualWidth - (2 * Space.S3)),
            _body.Pager.ActualHeight
        );
    }

    /// <summary>
    /// The foot of the panel (the panic strip): the status row and half the gap above it, so the red never touches the
    /// row above and «Release all» sits whole and centred in it (<see cref="Space.S1"/> + 44 + <see cref="Space.S1"/>).
    /// </summary>
    private Rect StatusRegion() => Span(Math.Max(0, FootTop() - Space.S1), _host.ActualHeight);

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
            if (FootCovers() != _footCovered)
            {
                // The notice card at the foot of the Compact body grew over «Qué ver», or went away.
                ApplyCover();
            }

            RefreshTargets();
            if (!_touching && IsVisible)
            {
                // The layers never move the grid: the space below it is the same with any of them open (D12).
                _ = _body.MeasureGridSpace(this, WorkBottom());
            }
        }
        finally
        {
            _measuring = false;
        }
    }

    private double WorkBottom() => WorkAreaBottom ?? SystemParameters.WorkArea.Bottom;

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
        ApplyFoot();
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

        if (_suggestionCard is not null && _suggestion is not null)
        {
            yield return new PanelTapTarget(_suggestionCard.CreateButton, _suggestion.Create);
            yield return new PanelTapTarget(_suggestionCard.NotNowButton, _suggestion.NotNow);
        }

        foreach (var target in _body.TapTargets.Concat(_body.PlacedTapTargets))
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

        // A layer on screen covers what is under it: only its own controls answer there (D12).
        var covers = _host
            .Layers.Where(static layer => layer.IsVisible)
            .Select(static layer => (Layer: layer, Bounds: TouchBounds.Of(layer, inflate: false)))
            .Where(static cover => !cover.Bounds.IsEmpty)
            .ToList();

        var added = new List<(FrameworkElement Element, int Id)>();

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

        // TAC-008: a tap on the band of test mode ends it, as the switch of Quick settings does (it had no target).
        if (
            _layers is { TestMode.IsOn: true } testing
            && _body.TestModeIndicator is { IsVisible: true } band
        )
        {
            Add(
                band,
                ElementIdOf(band),
                TouchTargetKind.Tap,
                new Target(null, testing.TestMode.Stop)
            );
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

        // A layer on screen also blocks the extra hit area of what it covers: a touch on its edge, or on its text, never
        // reaches the tile under it (TAC-002, D21 review); its own controls keep theirs.
        var occluders = covers
            .Select(cover => new TouchOccluder(
                cover.Bounds,
                [
                    .. added
                        .Where(a =>
                            ReferenceEquals(a.Element, cover.Layer)
                            || cover.Layer.IsAncestorOf(a.Element)
                        )
                        .Select(static a => new TouchTargetId(a.Id)),
                ]
            ))
            .ToImmutableArray();
        gestures.Recognizer.SetTargets(targets.ToImmutable(), occluders);

        // REG-02: every target answers on at least 44 × 44 with an invisible touch margin around its drawing. What a layer
        // covers is off the screen for UI Automation as well, so voice and Narrator never reach a tile under a card.
        void Add(FrameworkElement element, int id, TouchTargetKind kind, Target target)
        {
            var bounds = TouchBounds.Of(element, inflate: true);
            var covered = IsCovered(element);
            AutomationProperties.SetIsOffscreenBehavior(
                element,
                covered ? IsOffscreenBehavior.Offscreen : IsOffscreenBehavior.Default
            );
            if (bounds.IsEmpty || covered)
            {
                return;
            }

            targets.Add(new GestureTarget(new TouchTargetId(id), bounds, kind));
            added.Add((element, id));
            _targets[id] = target;
        }

        // Under a layer, a control only part of which shows is covered as well: a half tile must not send its keys.
        bool IsCovered(FrameworkElement element)
        {
            var drawn = TouchBounds.Of(element, inflate: false);
            return covers.Exists(cover =>
                !ReferenceEquals(cover.Layer, element)
                && !cover.Layer.IsAncestorOf(element)
                && Overlap(cover.Bounds, drawn)
            );
        }

        static bool Overlap(PhysicalRect a, PhysicalRect b) =>
            a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
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

    private static PhysicalRect PhysicalBounds(FrameworkElement element) =>
        TouchBounds.Of(element, inflate: false);

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
                // CUA-005: more than 60 px sideways turns the page; the recognizer then ignores taps for a moment. Under
                // the tile menu the swipe only closes it (PAN-008: the menu closes when the page changes), and under the
                // profile grid the hidden grid does not turn.
                if (_layers is { Menu.IsOpen: true } open)
                {
                    open.Menu.Close();
                }
                else if (!_viewModel.Picker.IsVisible)
                {
                    _body.Swiped(towardLeft: gesture.Swipe == SwipeDirection.Left);
                }

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
