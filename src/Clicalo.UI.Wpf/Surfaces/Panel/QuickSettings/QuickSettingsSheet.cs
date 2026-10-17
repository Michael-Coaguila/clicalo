using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel.QuickSettings;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;

/// <summary>
/// The Quick settings sheet (AJR-001) of direction C (D12): in the panel, a layer over its body at the level of the
/// panel (the <c>win</c> surface with the decorative line on top), so the panel keeps its size and nothing moves under
/// the finger. With <see cref="ShowsHeader"/>, a title row of <see cref="ControlHeight.Medium"/> with [quick] in
/// <c>heading</c> and «Cerrar», a 44 × 44 icon button. Below, the designed scroll (<see cref="ThemedScrollViewer"/>:
/// the thin bar in its own channel and the fade at the foot while there is more), with, in order: the way to the
/// Control Center (<see cref="NavRow"/>); Vista (three option cards); Opacidad with its value aligned with the controls
/// (− · slider · +); Tamaño (S, M, L); Lado de la pestaña (four icons, only in the Tab view); Tema (2 × 2); and the four
/// switch rows under a line. Every section has its <see cref="Overline"/> <see cref="Space.S2"/> above its controls and
/// sections are <see cref="Space.S4"/> apart. It only projects <see cref="QuickSettingsViewModel"/>.
/// </summary>
/// <remarks>
/// The panel registers <see cref="TapTargets"/> with its pointer layer (a tap calls the view model); UI Automation
/// Invoke, SelectionItem, RangeValue and Toggle reach the same view model through the controls. Every target is at
/// least 44 × 44 (REG-02). The panel's pointer layer consumes the finger, so the sheet follows it itself
/// (<see cref="Track"/>): a finger on the opacity track slides the value (AJR-002) and a vertical drag elsewhere scrolls
/// the sheet (AJR-001). The window beside the Tab bar limits the sheet to the free space of the work area
/// (<see cref="FitHeight"/>).
/// </remarks>
public sealed class QuickSettingsSheet : Border
{
    /// <summary>AJR-001: the sheet is at most 62 % of the screen high.</summary>
    public const double ScreenShare = 0.62;

    private readonly QuickSettingsViewModel _viewModel;
    private readonly DockPanel _header = new()
    {
        LastChildFill = true,
        Height = ControlHeight.Medium,
        Margin = new Thickness(Space.S3, 0, Space.S1, 0),
        Visibility = Visibility.Collapsed,
    };

    private readonly StyledText _title = PanelChrome.Text(TextStyleToken.Heading);
    private readonly IconButton _close = new()
    {
        Symbol = "close",
        Focusable = false,
        IsTabStop = false,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly NavRow _controlCenter;
    private readonly Overline _viewHeading = new();
    private readonly Overline _opacityHeading = new();
    private readonly StyledText _opacityValue = PanelChrome.Text(TextStyleToken.Label);
    private readonly Overline _sizeHeading = new();
    private readonly Overline _sideHeading = new();
    private readonly Overline _themeHeading = new();
    private readonly StackPanel _sideSection;
    private readonly StepSlider _opacity;
    private readonly List<(
        SegmentedControl Group,
        SegmentedItem Item,
        QuickOptionViewModel Option
    )> _options = [];
    private readonly List<(ToggleSwitch Row, QuickSwitchViewModel Switch)> _switches = [];
    private readonly ThemedScrollViewer _scroller;
    private readonly SegmentedControl _themes;
    private readonly bool _showsAlways;
    private double _screenLimit = double.PositiveInfinity;
    private uint? _contact;
    private bool _sliding;
    private bool _scrolling;
    private Point _origin;
    private double _startOffset;
    private bool _applying;

    /// <summary>Creates the sheet of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">Quick settings.</param>
    public QuickSettingsSheet(QuickSettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        BorderThickness = new Thickness(0, Stroke.Line, 0, 0);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));

        _title.VerticalAlignment = VerticalAlignment.Center;
        _close.Click += (_, _) => _viewModel.Close();
        DockPanel.SetDock(_close, Dock.Right);
        _header.Children.Add(_close);
        _header.Children.Add(_title);

        _controlCenter = new NavRow
        {
            Symbol = "settings",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = false,
            IsTabStop = false,
            Margin = new Thickness(0, Space.S1, 0, 0),
        };
        _controlCenter.Click += (_, _) => _viewModel.OpenControlCenter();

        _opacity = new StepSlider
        {
            Minimum = QuickSettingsViewModel.OpacityMinimumPercent,
            Maximum = QuickSettingsViewModel.OpacityMaximumPercent,
            SmallChange = 2 * QuickSettingsViewModel.OpacityStepPercent,
            LargeChange = QuickSettingsViewModel.OpacityStepPercent,
            TickFrequency = QuickSettingsViewModel.OpacityStepPercent,
            IsSnapToTickEnabled = true,
            Focusable = false,
            IsTabStop = false,
        };
        _opacity.ValueChanged += (_, change) =>
        {
            if (!_applying)
            {
                _viewModel.SetOpacityPercent(change.NewValue);
            }
        };

        // The value goes at the end of the heading, aligned with the controls under it (never against the scroll bar).
        var opacityTitle = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_opacityValue, Dock.Right);
        _opacityValue.VerticalAlignment = VerticalAlignment.Bottom;
        _opacityHeading.VerticalAlignment = VerticalAlignment.Bottom;
        opacityTitle.Children.Add(_opacityValue);
        opacityTitle.Children.Add(_opacityHeading);

        _sideSection = Section(_sideHeading, Group(viewModel.Sides, 4, OptionKind.Tile));
        var layout = new StackPanel { Margin = new Thickness(Space.S3, 0, Space.S1, Space.S6) };
        layout.Children.Add(_controlCenter);
        layout.Children.Add(Section(_viewHeading, Group(viewModel.Views, 3, OptionKind.Card)));
        layout.Children.Add(Section(opacityTitle, _opacity));
        layout.Children.Add(Section(_sizeHeading, Group(viewModel.Sizes, 3, OptionKind.Tile)));
        layout.Children.Add(_sideSection);
        _themes = Group(viewModel.Themes, 2, OptionKind.Tile);
        _themes.SizeChanged += (_, _) => FitThemes();
        layout.Children.Add(Section(_themeHeading, _themes));
        layout.Children.Add(Switches(viewModel.Switches));

        _scroller = new ThemedScrollViewer { PansWithMouse = false, Content = layout };
        var rows = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_header, Dock.Top);
        rows.Children.Add(_header);
        rows.Children.Add(_scroller);
        Child = rows;

        viewModel.PropertyChanged += OnChanged;
        foreach (var (_, _, option) in _options)
        {
            option.PropertyChanged += OnChanged;
        }

        foreach (var (_, item) in _switches)
        {
            item.PropertyChanged += OnChanged;
        }

        Refresh();
    }

    /// <summary>
    /// The close button, the way to the Control Center, every option, − and + of the opacity and the switch rows,
    /// while the sheet shows. A control scrolled out of sight is no target: its touch margin would otherwise take
    /// touches from the header or the status row (REG-02).
    /// </summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!_viewModel.IsOpen)
            {
                return [];
            }

            var targets = new List<PanelTapTarget>
            {
                new(_controlCenter, _viewModel.OpenControlCenter),
            };
            foreach (var (_, item, option) in _options)
            {
                targets.Add(new PanelTapTarget(item, option.Select));
            }

            if (_opacity.DecreaseButton is { } decrease)
            {
                targets.Add(new PanelTapTarget(decrease, _viewModel.DecreaseOpacity));
            }

            if (_opacity.IncreaseButton is { } increase)
            {
                targets.Add(new PanelTapTarget(increase, _viewModel.IncreaseOpacity));
            }

            foreach (var (row, item) in _switches)
            {
                targets.Add(new PanelTapTarget(row, item.Toggle));
            }

            var view = TouchBounds.Of(_scroller, inflate: false);
            var shown = targets.FindAll(target => InView(view, target.Element));
            if (_header.Visibility == Visibility.Visible)
            {
                shown.Insert(0, new PanelTapTarget(_close, _viewModel.Close));
            }

            return shown;
        }
    }

    /// <summary>
    /// Whether the sheet has its own title row with «Cerrar» (the panel: the sheet covers its body, so it says what it is
    /// and how to leave it).
    /// </summary>
    public bool ShowsHeader
    {
        get => _header.Visibility == Visibility.Visible;
        init => _header.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The «Cerrar» button of the title row.</summary>
    public IconButton CloseButton => _close;

    /// <summary>The scroll of the sheet.</summary>
    public ThemedScrollViewer Scroller => _scroller;

    /// <summary>Limits the sheet to 62 % of the screen it is on (AJR-001); the rest scrolls inside.</summary>
    /// <param name="screenHeight">The height of the monitor's work area, in device-independent pixels.</param>
    public void ApplyScreenHeight(double screenHeight)
    {
        _screenLimit = screenHeight > 0 ? screenHeight * ScreenShare : double.PositiveInfinity;
        MaxHeight = _screenLimit;
    }

    /// <summary>
    /// Whether the sheet shows whatever the view model says: in the window beside the bar of the Tab view (PES-009) the
    /// window itself appears and goes away, and a sheet that collapsed first would shrink it to its border for a moment.
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

    /// <summary>62 % of the height of the work area of its monitor (<see cref="ApplyScreenHeight"/>), in DIPs.</summary>
    public double ScreenLimit => _screenLimit;

    /// <summary>
    /// The height of the whole content of the sheet, its title row and border included, once it was laid out; null
    /// before. It is what the sheet takes when that is less than its limit.
    /// </summary>
    public double? NaturalHeight =>
        _scroller.ExtentHeight > 0
            ? _scroller.ExtentHeight
                + (ShowsHeader ? _header.Height : 0)
                + BorderThickness.Top
                + BorderThickness.Bottom
            : null;

    /// <summary>
    /// Limits the sheet to <paramref name="room"/>, the height left for it down to the bottom of the work area
    /// (<see cref="QuickSheetFit.Room"/>), and never above 62 % of the screen: what does not fit scrolls inside (AJR-001).
    /// </summary>
    /// <param name="room">The free height for the sheet, in device-independent pixels.</param>
    /// <returns>The height the sheet will have once laid out (<see cref="QuickSheetFit.Height"/>).</returns>
    public double FitHeight(double room)
    {
        var height = QuickSheetFit.MaxHeight(room, _screenLimit);
        if (!MaxHeight.Equals(height))
        {
            MaxHeight = height;
        }

        return QuickSheetFit.Height(MaxHeight, NaturalHeight);
    }

    /// <summary>
    /// Follows a finger, pen or mouse contact of the panel (AJR-001, AJR-002): one that goes down on the opacity track
    /// sets the value under it and slides it while it moves; one that goes down elsewhere on the sheet scrolls it once
    /// it moves vertically past <paramref name="thresholdPx"/>.
    /// </summary>
    /// <param name="sample">The pointer sample, in physical screen pixels.</param>
    /// <param name="thresholdPx">The drag threshold, in physical pixels.</param>
    /// <returns>Whether the contact slid or scrolled: it is not a tap.</returns>
    public bool Track(in PointerSample sample, double thresholdPx)
    {
        var at = new Point(sample.Position.X, sample.Position.Y);
        switch (sample.Phase)
        {
            case PointerPhase.Down when _contact is null && _viewModel.IsOpen:
                if (
                    _opacity.TrackElement is { } track
                    && InView(TouchBounds.Of(_scroller, inflate: false), track)
                    && TouchBounds.Of(track, inflate: true).Contains(sample.Position)
                )
                {
                    _contact = sample.PointerId;
                    _sliding = true;
                    Slide(at);
                    return true;
                }

                if (TouchBounds.Of(_scroller, inflate: false).Contains(sample.Position))
                {
                    _contact = sample.PointerId;
                    _origin = at;
                    _startOffset = _scroller.VerticalOffset;
                }

                return false;

            case PointerPhase.Move when _contact == sample.PointerId:
                if (_sliding)
                {
                    Slide(at);
                    return true;
                }

                var dy = at.Y - _origin.Y;
                _scrolling |= Math.Abs(dy) > thresholdPx;
                if (_scrolling)
                {
                    var scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
                    _scroller.ScrollToVerticalOffset(_startOffset - (dy / scale));
                }

                return _scrolling;

            case PointerPhase.Up
            or PointerPhase.Cancel when _contact == sample.PointerId:
                var used = _sliding || _scrolling;
                _contact = null;
                _sliding = false;
                _scrolling = false;
                return used;

            default:
                return false;
        }
    }

    /// <summary>Stops following the view model when the panel closes.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        foreach (var (_, _, option) in _options)
        {
            option.PropertyChanged -= OnChanged;
        }

        foreach (var (_, item) in _switches)
        {
            item.PropertyChanged -= OnChanged;
        }
    }

    /// <summary>
    /// Whether the center of <paramref name="element"/> shows inside <paramref name="view"/>, the scroller on screen;
    /// off screen (no window yet) nothing is filtered, and the panel finds no bounds for it anyway.
    /// </summary>
    private static bool InView(PhysicalRect view, FrameworkElement element) =>
        view.IsEmpty
        || (
            TouchBounds.Of(element, inflate: false) is { IsEmpty: false } bounds
            && view.Contains(bounds.Center)
        );

    private void Slide(Point screen)
    {
        if (
            _opacity.ValueAt(screen) is { } value
            && Math.Abs(value - _viewModel.OpacityPercent) > 0.5
        )
        {
            _viewModel.SetOpacityPercent(value);
        }
    }

    private static StackPanel Section(FrameworkElement heading, FrameworkElement content)
    {
        var section = new StackPanel { Margin = new Thickness(0, Space.S4, 0, 0) };
        heading.Margin = new Thickness(0, 0, 0, Space.S2);
        section.Children.Add(heading);
        section.Children.Add(content);
        return section;
    }

    private SegmentedControl Group(
        IReadOnlyList<QuickOptionViewModel> options,
        int columns,
        OptionKind kind
    )
    {
        var group = new SegmentedControl { Kind = kind, Columns = columns };
        foreach (var option in options)
        {
            var item = new SegmentedItem { Focusable = false, IsTabStop = false };
            if (option.Icon.Length > 0)
            {
                item.Symbol = option.Icon;
            }

            group.Items.Add(item);
            _options.Add((group, item, option));
        }

        group.SelectionChanged += (_, _) =>
        {
            if (_applying)
            {
                return;
            }

            // UI Automation SelectionItem.Select or the keyboard: the view model decides, then Refresh shows it.
            foreach (var (owner, item, option) in _options)
            {
                if (ReferenceEquals(owner, group) && item.IsSelected && !option.IsSelected)
                {
                    option.Select();
                    break;
                }
            }

            Refresh();
        };
        return group;
    }

    private Border Switches(IReadOnlyList<QuickSwitchViewModel> switches)
    {
        var rows = new StackPanel { Margin = new Thickness(0, Space.S1, 0, 0) };
        foreach (var item in switches)
        {
            var row = new ToggleSwitch
            {
                Symbol = item.Icon,
                Focusable = false,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            row.Checked += (_, _) => Toggled(item, on: true);
            row.Unchecked += (_, _) => Toggled(item, on: false);
            rows.Children.Add(row);
            _switches.Add((row, item));
        }

        var block = new Border
        {
            Child = rows,
            Margin = new Thickness(0, Space.S3, 0, 0),
            BorderThickness = new Thickness(0, Stroke.Line, 0, 0),
        };
        block.SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        return block;
    }

    private void Toggled(QuickSwitchViewModel item, bool on)
    {
        // UI Automation Toggle or the keyboard flipped the row: the view model decides, then Refresh shows it.
        if (!_applying && item.IsOn != on)
        {
            item.Toggle();
            Refresh();
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs change) => Refresh();

    /// <summary>
    /// The themes go in two columns, as in the mockup, while every name fits whole beside its swatch; in a
    /// narrower sheet (the Tab view, S, a larger text) they go one under the other, never cut with «…».
    /// </summary>
    private void FitThemes()
    {
        if (_themes.ActualWidth <= 0)
        {
            return;
        }

        // The group spreads Space.S1 past each side and every cell keeps Space.S1 around its option.
        var cell = (_themes.ActualWidth / 2) - (2 * Space.S1);
        var fits = true;
        foreach (var (owner, item, _) in _options)
        {
            if (
                ReferenceEquals(owner, _themes)
                && item.Content is DockPanel row
                && item.Padding.Left + item.Padding.Right + ThemeSwatch.WholeWidth(row) > cell
            )
            {
                fits = false;
                break;
            }
        }

        var columns = fits ? 2 : 1;
        if (_themes.Columns != columns)
        {
            _themes.Columns = columns;
        }
    }

    private void Refresh()
    {
        _applying = true;
        try
        {
            Visibility =
                _showsAlways || _viewModel.IsOpen ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(this, _viewModel.Name);
            _title.Text = _viewModel.Name;
            AutomationProperties.SetName(_close, _viewModel.CloseName);
            _controlCenter.Content = _viewModel.ControlCenterTitle;
            _controlCenter.Caption = _viewModel.ControlCenterSubtitle;
            AutomationProperties.SetName(_controlCenter, _viewModel.ControlCenterTitle);
            AutomationProperties.SetHelpText(_controlCenter, _viewModel.ControlCenterSubtitle);
            _viewHeading.Text = _viewModel.ViewHeading;
            _opacityHeading.Text = _viewModel.OpacityHeading;
            _opacityValue.Text = _viewModel.OpacityText;
            _sizeHeading.Text = _viewModel.SizeHeading;
            _sideHeading.Text = _viewModel.SideHeading;
            _themeHeading.Text = _viewModel.ThemeHeading;
            _sideSection.Visibility = _viewModel.ShowsSides
                ? Visibility.Visible
                : Visibility.Collapsed;
            _opacity.Value = _viewModel.OpacityPercent;
            _opacity.DecreaseName = _viewModel.OpacityLessName;
            _opacity.IncreaseName = _viewModel.OpacityMoreName;
            AutomationProperties.SetName(_opacity, _viewModel.OpacityHeading);

            foreach (var (_, item, option) in _options)
            {
                // A theme shows its swatch (filled with ✓ while chosen), as in «General y panel» and the welcome.
                item.Content =
                    option.Value is ThemeChoice theme
                        ? ThemeSwatch.Row(theme, option.Label, option.IsSelected)
                    : option.Label.Length > 0 ? option.Label
                    : null;
                if (option.Value is ThemeChoice)
                {
                    item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                }

                item.IsSelected = option.IsSelected;
                AutomationProperties.SetName(item, option.AccessibleName);
            }

            foreach (var (row, item) in _switches)
            {
                row.Content = item.Label;
                row.IsChecked = item.IsOn;
            }
        }
        finally
        {
            _applying = false;
        }

        FitThemes();
    }
}
