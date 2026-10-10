using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel.QuickSettings;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;

/// <summary>
/// The Quick settings sheet of the panel (AJR-001, docs/04 §5, prototype lines 77–126): a <c>card</c> sheet of radius
/// 12 under the search, margin 0 12 10 12, padding 10, sections 14 apart, at most 62 % of the screen high with its own
/// scroll. In order: the «Centro de control» card (56 high, accent outline on accentWash, a 36 px accent tile with the
/// <c>settings</c> icon, [openCC] in 14 bold, [ccSub] in 11 muted and a chevron); Vista (three 52 px options with
/// icons); Opacidad with its value in accent (− · slider · +); Tamaño (S, M, L); Lado de la pestaña (four icons, only
/// in the Tab view); Tema (2 × 2); and the four switch rows. It only projects <see cref="QuickSettingsViewModel"/>.
/// </summary>
/// <remarks>
/// The panel registers <see cref="TapTargets"/> with its pointer layer (a tap calls the view model); UI Automation
/// Invoke, SelectionItem, RangeValue and Toggle reach the same view model through the controls. Every target is at
/// least 44 × 44 (REG-02). The panel's pointer layer consumes the finger, so the sheet follows it itself
/// (<see cref="Track"/>): a finger on the opacity track slides the value (AJR-002) and a vertical drag elsewhere scrolls
/// the sheet (AJR-001). The panel also limits the sheet to the free space of the work area (<see cref="FitHeight"/>).
/// </remarks>
public sealed class QuickSettingsSheet : Border
{
    /// <summary>AJR-001: the sheet is at most 62 % of the screen high.</summary>
    public const double ScreenShare = 0.62;

    private const double SectionGap = 14;
    private const double HeadingGap = 6;
    private const double CardHeight = 56;
    private const double CardIconTile = 36;
    private const double ViewOptionHeight = 52;
    private const double OptionHeight = 40;
    private const double HeadingPx = 12;

    /// <summary>The text of the options of Vista and Tema (prototype: 12 px); the sizes keep the 13 of the control.</summary>
    private const double SmallOptionPx = 12;

    /// <summary>The sheet never gets lower than this to fit the work area: the rest scrolls inside.</summary>
    private const double MinimumFitHeight = 120;

    private readonly QuickSettingsViewModel _viewModel;
    private readonly TouchButton _controlCenter;
    private readonly TextBlock _ccTitle = new()
    {
        FontWeight = FontWeights.Bold,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly TextBlock _ccSubtitle = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _viewHeading = Heading();
    private readonly TextBlock _opacityHeading = Heading();
    private readonly TextBlock _opacityValue = new()
    {
        VerticalAlignment = VerticalAlignment.Bottom,
    };
    private readonly TextBlock _sizeHeading = Heading();
    private readonly TextBlock _sideHeading = Heading();
    private readonly TextBlock _themeHeading = Heading();
    private readonly StackPanel _sideSection;
    private readonly StepSlider _opacity;
    private readonly List<(
        SegmentedControl Group,
        SegmentedItem Item,
        QuickOptionViewModel Option
    )> _options = [];
    private readonly List<(ToggleSwitch Row, QuickSwitchViewModel Switch)> _switches = [];
    private readonly ScrollViewer _scroller;
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
        Margin = new Thickness(12, 0, 12, 10);
        Padding = new Thickness(10);
        CornerRadius = new CornerRadius(Radii.Tile);
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));

        _controlCenter = ControlCenterCard();
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

        var opacityTitle = new DockPanel();
        DockPanel.SetDock(_opacityValue, Dock.Right);
        opacityTitle.Children.Add(_opacityValue);
        opacityTitle.Children.Add(_opacityHeading);
        _opacityValue.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
        _opacityValue.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(13));
        _opacityValue.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );

        _sideSection = Section(_sideHeading, Group(viewModel.Sides, 4, OptionHeight));
        var layout = new StackPanel();
        layout.Children.Add(_controlCenter);
        layout.Children.Add(
            Section(_viewHeading, Group(viewModel.Views, 3, ViewOptionHeight, SmallOptionPx))
        );
        layout.Children.Add(Section(opacityTitle, _opacity));
        layout.Children.Add(Section(_sizeHeading, Group(viewModel.Sizes, 3, OptionHeight)));
        layout.Children.Add(_sideSection);
        layout.Children.Add(
            Section(_themeHeading, Group(viewModel.Themes, 2, OptionHeight, SmallOptionPx))
        );
        layout.Children.Add(Switches(viewModel.Switches));

        _scroller = new SurfaceScrollViewer { Content = layout };
        Child = _scroller;

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
    /// The card, every option, − and + of the opacity and the switch rows, while the sheet shows. A control scrolled
    /// out of sight is no target: its touch margin would otherwise take touches from the header or the grid (REG-02).
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
            return targets.FindAll(target => InView(view, target.Element));
        }
    }

    /// <summary>Limits the sheet to 62 % of the screen it is on (AJR-001); the rest scrolls inside.</summary>
    /// <param name="screenHeight">The height of the monitor's work area, in device-independent pixels.</param>
    public void ApplyScreenHeight(double screenHeight)
    {
        _screenLimit = screenHeight > 0 ? screenHeight * ScreenShare : double.PositiveInfinity;
        MaxHeight = _screenLimit;
    }

    /// <summary>
    /// Limits the sheet to <paramref name="room"/>, the height left for it down to the bottom of the work area, and never
    /// above 62 % of the screen: the panel never leaves the screen, and what does not fit scrolls inside (AJR-001).
    /// </summary>
    /// <param name="room">The free height for the sheet, in device-independent pixels.</param>
    public void FitHeight(double room)
    {
        var height = Math.Min(_screenLimit, Math.Max(MinimumFitHeight, room));
        if (Math.Abs(MaxHeight - height) > 0.5)
        {
            MaxHeight = height;
        }
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

    private static TextBlock Heading()
    {
        var heading = new TextBlock { FontWeight = FontWeights.Bold };
        heading.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(HeadingPx));
        heading.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        return heading;
    }

    private static StackPanel Section(UIElement heading, UIElement content)
    {
        var section = new StackPanel { Margin = new Thickness(0, SectionGap, 0, 0) };
        section.Children.Add(heading);
        if (content is FrameworkElement element)
        {
            element.Margin = new Thickness(0, HeadingGap, 0, 0);
        }

        section.Children.Add(content);
        return section;
    }

    private TouchButton ControlCenterCard()
    {
        var tile = new Border
        {
            Width = CardIconTile,
            Height = CardIconTile,
            CornerRadius = new CornerRadius(Radii.Control),
            Child = new SymbolIcon
            {
                Symbol = "settings",
                Size = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        tile.SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Accent));
        ((SymbolIcon)tile.Child).SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.OnAccent)
        );

        _ccTitle.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(14));
        _ccSubtitle.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(11));
        _ccSubtitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        var texts = new StackPanel
        {
            Margin = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        texts.Children.Add(_ccTitle);
        texts.Children.Add(_ccSubtitle);

        var chevron = new SymbolIcon
        {
            Symbol = "chevron_right",
            Size = 22,
            VerticalAlignment = VerticalAlignment.Center,
        };
        chevron.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );

        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(tile, Dock.Left);
        DockPanel.SetDock(chevron, Dock.Right);
        row.Children.Add(tile);
        row.Children.Add(chevron);
        row.Children.Add(texts);

        var card = new TouchButton
        {
            Appearance = ButtonAppearance.Outline,
            MinHeight = CardHeight,
            Padding = new Thickness(8, 8, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            FontWeight = FontWeights.Normal,
            Focusable = false,
            IsTabStop = false,
            Content = row,
        };
        card.SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.AccentWash));
        card.SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Accent));
        return card;
    }

    private SegmentedControl Group(
        IReadOnlyList<QuickOptionViewModel> options,
        int columns,
        double height,
        double textPx = 0
    )
    {
        var group = new SegmentedControl { Columns = columns };
        foreach (var option in options)
        {
            var item = new SegmentedItem
            {
                Height = height,
                Focusable = false,
                IsTabStop = false,
            };
            if (textPx > 0)
            {
                // Three options share the width of the smallest panel: their names fit whole, also with the thicker
                // borders of high contrast.
                item.FontSize = textPx;
                item.Padding = new Thickness(2, 4, 2, 4);
            }

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

    private StackPanel Switches(IReadOnlyList<QuickSwitchViewModel> switches)
    {
        var rows = new StackPanel { Margin = new Thickness(0, SectionGap, 0, 0) };
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

        return rows;
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

    private void Refresh()
    {
        _applying = true;
        try
        {
            Visibility = _viewModel.IsOpen ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(this, _viewModel.Name);
            _ccTitle.Text = _viewModel.ControlCenterTitle;
            _ccSubtitle.Text = _viewModel.ControlCenterSubtitle;
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
                item.Content = option.Label.Length > 0 ? option.Label : null;
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
    }
}
