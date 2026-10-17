using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The profile grid (SEL-003) in direction C (D12): in the panel, a layer over the grid of shortcuts and its pager (the
/// <c>win</c> surface with the decorative line on top), so the panel keeps its size. One option card of
/// <see cref="ControlHeight.Large"/> per profile (its icon over its name with «…»), <see cref="Space.S2"/> apart in as
/// many columns as fit; the profile in view has the single look of «elegido» and the one of the app in front a dot of
/// <see cref="Space.S2"/> in its corner, which the legend explains. Then «Crear para {app}» when it applies and «+ Más»
/// in a dashed slot. Opened from the title of the header it starts with ★ Frequents (SEL-006). It scrolls with the
/// designed scroll (<see cref="ThemedScrollViewer"/>): a finger that moves past the cancel distance scrolls it and
/// chooses nothing (TAC-004, <see cref="Track"/>), and a card scrolled out of view is not a target. It only projects
/// <see cref="PickerGridViewModel"/>.
/// </summary>
public sealed class PickerGridView : Border
{
    private static readonly DoubleCollection Dash = Frozen();

    private readonly PickerGridViewModel _viewModel;
    private readonly bool _compact;
    private readonly UniformGrid _grid = new() { Margin = new Thickness(-Space.S1) };
    private readonly TouchButton _suggestion;
    private readonly TouchButton _more;
    private readonly Grid _moreSlot = new() { Margin = new Thickness(Space.S1) };
    private readonly OptionButton _frequents;
    private readonly ThemedScrollViewer _scroller;
    private readonly bool _showsAlways;
    private readonly PanScroll _pan = new();
    private readonly StyledText _legend = PanelChrome.Text(
        TextStyleToken.Caption,
        ColorToken.Muted
    );
    private readonly DockPanel _legendRow;
    private readonly StackPanel _content;
    private readonly List<(PickerEntryViewModel Entry, OptionButton Control, Grid Cell)> _entries =
    [];

    /// <summary>Creates the profile grid of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The profile grid.</param>
    /// <param name="compact">The Compact view (narrower cards).</param>
    public PickerGridView(PickerGridViewModel viewModel, bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _compact = compact;
        BorderThickness = new Thickness(0, Stroke.Line, 0, 0);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));

        _suggestion = new TouchButton
        {
            Variant = ButtonVariant.Secondary,
            Symbol = "add_circle",
            Size = ControlSize.Large,
            MaxLines = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(Space.S1),
            Focusable = false,
            IsTabStop = false,
        };
        _suggestion.Click += (_, _) => _viewModel.CreateSuggested();

        var outline = new Rectangle
        {
            RadiusX = Radii.Medium,
            RadiusY = Radii.Medium,
            StrokeDashArray = Dash,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false,
        };
        outline.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));
        outline.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);
        _more = new TouchButton
        {
            Variant = ButtonVariant.Tertiary,
            Symbol = "add",
            Size = ControlSize.Large,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = false,
            IsTabStop = false,
        };
        _more.Click += (_, _) => _viewModel.More();
        _moreSlot.Children.Add(outline);
        _moreSlot.Children.Add(_more);

        // SEL-006: ★ Frequents, first in the grid while the selector row is not there to offer it.
        _frequents = Option("star");
        _frequents.Click += (_, _) => _viewModel.Frequents();

        var mark = PanelChrome.Mark(ColorToken.AccentInk);
        mark.Visibility = Visibility.Visible;
        mark.Margin = new Thickness(0, 0, Space.S2, 0);
        _legendRow = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, Space.S3, 0, 0),
        };
        var legend = _legendRow;
        DockPanel.SetDock(mark, Dock.Left);
        legend.Children.Add(mark);
        _legend.VerticalAlignment = VerticalAlignment.Center;
        legend.Children.Add(_legend);

        _content = new StackPanel
        {
            Margin = new Thickness(Space.S3, Space.S3, Space.S1, Space.S6),
        };
        _content.Children.Add(_grid);
        _content.Children.Add(legend);
        _scroller = new ThemedScrollViewer { PansWithMouse = false, Content = _content };
        Child = _scroller;

        viewModel.PropertyChanged += OnChanged;
        viewModel.Entries.CollectionChanged += OnEntriesChanged;
        SizeChanged += (_, _) => Arrange();
        Rebuild();
    }

    /// <summary>
    /// Every card of the grid while it shows; a card scrolled out of the view is left out, so a touch under or over the
    /// grid never chooses a profile nobody sees (TAC-004).
    /// </summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!Shown)
            {
                return [];
            }

            var targets = new List<PanelTapTarget>();
            if (_viewModel.HasFrequents)
            {
                targets.Add(new PanelTapTarget(_frequents, _viewModel.Frequents));
            }

            foreach (var (entry, control, _) in _entries)
            {
                targets.Add(new PanelTapTarget(control, entry.Choose));
            }

            if (_viewModel.HasSuggestion)
            {
                targets.Add(new PanelTapTarget(_suggestion, _viewModel.CreateSuggested));
            }

            targets.Add(new PanelTapTarget(_more, _viewModel.More));
            var view = TouchBounds.Of(_scroller, inflate: false);
            return targets.FindAll(target => InView(view, target.Element));
        }
    }

    /// <summary>
    /// Whether the grid shows whatever the panel says: the window beside the bar of the Tab view (PES-010) is the grid
    /// alone, so it is laid out whole the first time it is measured instead of growing once the panel opens its grid.
    /// </summary>
    public bool ShowsAlways
    {
        get => _showsAlways;
        init
        {
            _showsAlways = value;
            Refresh();
        }
    }

    /// <summary>
    /// Whether the content keeps its own inset inside the panel (true by default). The window beside the bar of the Tab
    /// view turns it off: its padding is the inset, so the cards line up with its header (PES-010).
    /// </summary>
    public bool IsInset
    {
        get => _content.Margin.Left > 0;
        init =>
            _content.Margin = value
                ? new Thickness(Space.S3, Space.S3, Space.S1, Space.S6)
                : new Thickness(0, 0, 0, Space.S6);
    }

    /// <summary>The ★ Frequents card (SEL-006).</summary>
    public OptionButton FrequentsTile => _frequents;

    /// <summary>The scroll viewer of the grid (TAC-004).</summary>
    public ScrollViewer Scroller => _scroller;

    /// <summary>
    /// Follows a finger, pen or mouse contact of the panel (TAC-004): one that goes down on the grid and moves
    /// vertically past <paramref name="thresholdPx"/> scrolls it, and is no longer a tap.
    /// </summary>
    /// <param name="sample">The pointer sample, in physical screen pixels.</param>
    /// <param name="thresholdPx">The cancel distance, in physical pixels.</param>
    /// <returns>Whether the contact scrolled the grid: it chooses nothing.</returns>
    public bool Track(in PointerSample sample, double thresholdPx)
    {
        switch (sample.Phase)
        {
            case PointerPhase.Down when _viewModel.IsVisible && !_pan.IsTracking:
                if (TouchBounds.Of(_scroller, inflate: false).Contains(sample.Position))
                {
                    _ = _pan.Down(sample.PointerId, sample.Position.Y, _scroller.VerticalOffset);
                }

                return false;

            case PointerPhase.Move:
                if (
                    _pan.Move(
                        sample.PointerId,
                        sample.Position.Y,
                        thresholdPx,
                        VisualTreeHelper.GetDpi(this).DpiScaleY
                    )
                    is not { } offset
                )
                {
                    return false;
                }

                _scroller.ScrollToVerticalOffset(offset);
                return true;

            case PointerPhase.Up
            or PointerPhase.Cancel:
                return _pan.Up(sample.PointerId);

            default:
                return false;
        }
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
        foreach (var (entry, _, _) in _entries)
        {
            entry.PropertyChanged -= OnChanged;
        }
    }

    private static bool InView(PhysicalRect view, FrameworkElement element) =>
        view.IsEmpty
        || (
            TouchBounds.Of(element, inflate: false) is { IsEmpty: false } bounds
            && view.Contains(bounds.Center)
        );

    private static DoubleCollection Frozen()
    {
        var dash = new DoubleCollection([Stroke.Selected, Stroke.Selected]);
        dash.Freeze();
        return dash;
    }

    private static OptionButton Option(string? symbol) =>
        new()
        {
            Kind = OptionKind.Card,
            Symbol = symbol,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = false,
            IsTabStop = false,
        };

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        foreach (var (entry, _, _) in _entries)
        {
            entry.PropertyChanged -= OnChanged;
        }

        _entries.Clear();
        foreach (var entry in _viewModel.Entries)
        {
            var control = Option(null);
            var chosen = entry;
            control.Click += (_, _) => chosen.Choose();
            entry.PropertyChanged += OnChanged;

            // The dot of the app in front goes in a corner of its own, never over the icon or the name.
            var dot = PanelChrome.Mark(ColorToken.AccentInk);
            dot.HorizontalAlignment = HorizontalAlignment.Right;
            dot.VerticalAlignment = VerticalAlignment.Top;
            dot.Margin = new Thickness(0, Space.S2, Space.S2, 0);
            var cell = new Grid { Margin = new Thickness(Space.S1) };
            cell.Children.Add(control);
            cell.Children.Add(dot);
            _entries.Add((entry, control, cell));
        }

        Refresh();
    }

    private bool Shown => _showsAlways || _viewModel.IsVisible;

    private void Refresh()
    {
        Visibility = Shown ? Visibility.Visible : Visibility.Collapsed;
        _suggestion.Content = _viewModel.SuggestionName;
        AutomationProperties.SetName(_suggestion, _viewModel.SuggestionName);
        _more.Content = _viewModel.MoreName;
        AutomationProperties.SetName(_more, _viewModel.MoreName);
        _legend.Text = _viewModel.Legend;

        // The legend explains the dot only while a card shows it.
        _legendRow.Visibility = _entries.Exists(static e => e.Entry.IsActiveApp)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _frequents.Content = Label(_viewModel.FrequentsName);
        _frequents.IsSelected = _viewModel.IsFrequentsCurrent;
        AutomationProperties.SetName(_frequents, _viewModel.FrequentsName);
        AutomationProperties.SetItemStatus(_frequents, _viewModel.FrequentsState);
        _frequents.Margin = new Thickness(Space.S1);

        foreach (var (entry, control, cell) in _entries)
        {
            control.Symbol = entry.Icon.Length == 0 ? null : entry.Icon;
            control.Content = Label(entry.Name);
            control.IsSelected = entry.IsCurrent;
            AutomationProperties.SetName(control, entry.Name);
            AutomationProperties.SetItemStatus(control, entry.AccessibleState);
            cell.Children[1].Visibility = entry.IsActiveApp
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        Arrange();
    }

    private static StyledText Label(string text) =>
        new(text, TextStyleToken.Label)
        {
            MaxLines = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

    /// <summary>Lays the cards out in as many columns as fit (SEL-003), the extra ones last.</summary>
    private void Arrange()
    {
        var width =
            (_scroller.ViewportWidth > 0 ? _scroller.ViewportWidth : ActualWidth)
            - Space.S3
            - Space.S1;
        _grid.Columns = PickerLayout.Columns(width > 0 ? width : double.NaN, _compact);
        _grid.Children.Clear();
        if (_viewModel.HasFrequents)
        {
            _grid.Children.Add(_frequents);
        }

        foreach (var (_, _, cell) in _entries)
        {
            _grid.Children.Add(cell);
        }

        if (_viewModel.HasSuggestion)
        {
            _grid.Children.Add(_suggestion);
        }

        _grid.Children.Add(_moreSlot);
    }
}
