using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The profile grid (SEL-003), under the selector in the Full view: a card with a tile per profile (76 high, at least 88
/// wide; icon in accent and the name in 13 px cut with «…»), accentWash and a 2 px accent outline on the profile in
/// view, a 9 px dot on the profile of the active app, «Crear para {app}» in warn when it applies, «+ Más» with a
/// dashed outline, and the legend of the dot. Opened from the title of the header it starts with ★ Frequents
/// (SEL-006). It scrolls inside <see cref="FrameworkElement.MaxHeight"/>, which the surface sets to 46 % of the work
/// area: a finger that moves past the cancel distance scrolls it and chooses nothing (TAC-004, <see cref="Track"/>),
/// and a tile scrolled out of view is not a target. It only projects <see cref="PickerGridViewModel"/>.
/// </summary>
public sealed class PickerGridView : Border
{
    private const double IconPx = 24;
    private const double ExtraIconPx = 22;
    private const double NamePx = 13;
    private const double ExtraPx = 12;
    private const double DotPx = 9;
    private const double LegendDotPx = 8;
    private const double LegendPx = 11;
    private const double ActiveBorder = 2;
    private const double DashedBorder = 2;

    private readonly PickerGridViewModel _viewModel;
    private readonly bool _compact;
    private readonly UniformGrid _grid = new();
    private readonly ShortcutTile _suggestion;
    private readonly TextBlock _suggestionText = new()
    {
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly ShortcutTile _more;
    private readonly TextBlock _moreText = new()
    {
        FontWeight = FontWeights.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private readonly ShortcutTile _frequents;
    private readonly TextBlock _frequentsText = new()
    {
        FontWeight = FontWeights.Bold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 5, 0, 0),
    };

    private readonly ScrollViewer _scroller;
    private readonly PanScroll _pan = new();
    private readonly TextBlock _legend = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly List<(PickerEntryViewModel Entry, ShortcutTile Control)> _entries = [];

    /// <summary>Creates the profile grid of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The profile grid.</param>
    /// <param name="compact">The Compact view (tiles at least 80 wide).</param>
    public PickerGridView(PickerGridViewModel viewModel, bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _compact = compact;
        Margin = new Thickness(12, 0, 12, 10);
        Padding = new Thickness(8);
        CornerRadius = new CornerRadius(Radii.LargeCard);
        PanelChrome.SetBrush(this, BackgroundProperty, ColorToken.Card);
        PanelChrome.SetBrush(this, BorderBrushProperty, ColorToken.Border);
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);

        _suggestion = Extra("add_circle", _suggestionText, ColorToken.Warn, dashed: false);
        PanelChrome.Paint(_suggestion, ColorToken.WarnWash, ColorToken.Text, ColorToken.Warn);
        _suggestion.Invoked += (_, _) => _viewModel.CreateSuggested();
        _more = Extra("add", _moreText, ColorToken.Muted, dashed: true);
        PanelChrome.Paint(_more, null, ColorToken.Muted, null);
        _more.Invoked += (_, _) => _viewModel.More();

        // SEL-006: ★ Frequents, first in the grid while the selector row is not there to offer it.
        _frequents = NewTile();
        var star = new SymbolIcon
        {
            Symbol = "star",
            Size = IconPx,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        star.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );
        _frequentsText.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(NamePx));
        var frequents = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        frequents.Children.Add(star);
        frequents.Children.Add(_frequentsText);
        _frequents.Tag = frequents;
        _frequents.Invoked += (_, _) => _viewModel.Frequents();

        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(4, 6, 4, 2),
        };
        var legendDot = new Border
        {
            Width = LegendDotPx,
            Height = LegendDotPx,
            CornerRadius = new CornerRadius(LegendDotPx / 2),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        PanelChrome.SetBrush(legendDot, Border.BackgroundProperty, ColorToken.Accent);
        _legend.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(LegendPx));
        _legend.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        legend.Children.Add(legendDot);
        legend.Children.Add(_legend);

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(_grid);
        stack.Children.Add(legend);
        _scroller = new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };
        Child = _scroller;

        viewModel.PropertyChanged += OnChanged;
        viewModel.Entries.CollectionChanged += OnEntriesChanged;
        SizeChanged += (_, _) => Arrange();
        Rebuild();
    }

    /// <summary>
    /// Every tile of the grid while it shows; a tile scrolled out of the view is left out, so a touch under or over
    /// the grid never chooses a profile nobody sees (TAC-004).
    /// </summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!_viewModel.IsVisible)
            {
                return [];
            }

            var targets = new List<PanelTapTarget>();
            if (_viewModel.HasFrequents)
            {
                targets.Add(new PanelTapTarget(_frequents, _viewModel.Frequents));
            }

            foreach (var (entry, control) in _entries)
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

    /// <summary>The ★ Frequents tile (SEL-006).</summary>
    public ShortcutTile FrequentsTile => _frequents;

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
        foreach (var (entry, _) in _entries)
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

    private static ShortcutTile NewTile()
    {
        var tile = PanelChrome.NewButton(PanelChrome.Large, ShortcutTilePattern.Invoke);
        tile.Height = PanelSizes.Layout.PanelProfileTileHeightPx;
        tile.Margin = new Thickness(PickerLayout.GapPx / 2.0);
        tile.Padding = new Thickness(4, 6, 4, 6);
        tile.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        tile.VerticalContentAlignment = VerticalAlignment.Stretch;
        return tile;
    }

    private static ShortcutTile Extra(
        string symbol,
        TextBlock text,
        ColorToken iconColor,
        bool dashed
    )
    {
        var tile = NewTile();
        var icon = new SymbolIcon
        {
            Symbol = symbol,
            Size = ExtraIconPx,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        icon.SetResourceReference(SymbolIcon.ForegroundProperty, ThemeBrushKey.For(iconColor));
        text.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(ExtraPx));
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = tile.Padding,
        };
        stack.Children.Add(icon);
        stack.Children.Add(text);
        var layered = new Grid();
        if (dashed)
        {
            // «+ Más» has a dashed outline (SEL-003); a WPF border cannot dash, so a rectangle draws it.
            var outline = new Rectangle
            {
                RadiusX = Radii.Tile,
                RadiusY = Radii.Tile,
                StrokeThickness = DashedBorder,
                StrokeDashArray = new DoubleCollection([3, 2]),
            };
            outline.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));
            layered.Children.Add(outline);
        }

        layered.Children.Add(stack);
        tile.Padding = new Thickness(0);
        tile.Tag = layered;
        return tile;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        foreach (var (entry, _) in _entries)
        {
            entry.PropertyChanged -= OnChanged;
        }

        _entries.Clear();
        foreach (var entry in _viewModel.Entries)
        {
            var control = NewTile();
            var chosen = entry;
            control.Invoked += (_, _) => chosen.Choose();
            entry.PropertyChanged += OnChanged;
            _entries.Add((entry, control));
        }

        Refresh();
    }

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _suggestionText.Text = _viewModel.SuggestionName;
        _suggestion.AccessibleName = _viewModel.SuggestionName;
        _moreText.Text = _viewModel.MoreName;
        _more.AccessibleName = _viewModel.MoreName;
        _legend.Text = _viewModel.Legend;
        _frequentsText.Text = _viewModel.FrequentsName;
        _frequents.AccessibleName = _viewModel.FrequentsName;
        _frequents.AccessibleState = _viewModel.FrequentsState;
        if (_viewModel.IsFrequentsCurrent)
        {
            PanelChrome.Paint(
                _frequents,
                ColorToken.AccentWash,
                ColorToken.Text,
                ColorToken.Accent
            );
            _frequents.BorderThickness = new Thickness(ActiveBorder);
        }
        else
        {
            PanelChrome.Paint(_frequents, ColorToken.Side, ColorToken.Text, ColorToken.Border);
        }

        foreach (var (entry, control) in _entries)
        {
            control.AccessibleName = entry.Name;
            control.AccessibleState = entry.AccessibleState;
            control.Tag = EntryContent(entry);
            if (entry.IsCurrent)
            {
                PanelChrome.Paint(
                    control,
                    ColorToken.AccentWash,
                    ColorToken.Text,
                    ColorToken.Accent
                );
                control.BorderThickness = new Thickness(ActiveBorder);
            }
            else
            {
                PanelChrome.Paint(control, ColorToken.Side, ColorToken.Text, ColorToken.Border);
            }
        }

        Arrange();
    }

    private static Grid EntryContent(PickerEntryViewModel entry)
    {
        var grid = new Grid();
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var icon = new SymbolIcon
        {
            Symbol = entry.Icon,
            Size = IconPx,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );
        var name = new TextBlock
        {
            Text = entry.Name,
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0),
        };
        name.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(NamePx));
        stack.Children.Add(icon);
        stack.Children.Add(name);
        grid.Children.Add(stack);
        if (entry.IsActiveApp)
        {
            var dot = new Border
            {
                Width = DotPx,
                Height = DotPx,
                CornerRadius = new CornerRadius(DotPx / 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            PanelChrome.SetBrush(dot, Border.BackgroundProperty, ColorToken.Accent);
            grid.Children.Add(dot);
        }

        return grid;
    }

    /// <summary>Lays the tiles out in as many columns as fit (SEL-003), the extra tiles last.</summary>
    private void Arrange()
    {
        var width =
            ActualWidth
            - Padding.Left
            - Padding.Right
            - BorderThickness.Left
            - BorderThickness.Right;
        _grid.Columns = PickerLayout.Columns(width > 0 ? width : double.NaN, _compact);
        _grid.Children.Clear();
        if (_viewModel.HasFrequents)
        {
            _grid.Children.Add(_frequents);
        }

        foreach (var (_, control) in _entries)
        {
            _grid.Children.Add(control);
        }

        if (_viewModel.HasSuggestion)
        {
            _grid.Children.Add(_suggestion);
        }

        _grid.Children.Add(_more);
    }
}
