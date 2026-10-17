using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.Header;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The row «Qué ver» of direction C (D12, SEL-001, CAB-003): a track of <see cref="ControlHeight.Medium"/> in <c>side</c>
/// with two segments of the same width, ★ Frequents and the profile (its icon, its name with «…», the dot of the app in
/// front, the yellow dot of a pending suggestion and ▾, ▴ or ↶), and at its end the Auto/Fixed button of
/// <see cref="ControlHeight.Medium"/> × <see cref="ControlHeight.Medium"/>, next to what it controls. The segment in view
/// has the single look of «elegido». ★ Frequents is an Invoke button and the profile an ExpandCollapse one for UI
/// Automation; both say their dots in their item status, never by color alone. The Full view shows it above the grid
/// and the Compact view at its foot (VCO-002), where the open profile grid closes with ✕. It only projects
/// <see cref="SelectorRowViewModel"/> and, for Auto/Fixed, <see cref="PanelHeaderViewModel"/>.
/// </summary>
public sealed class SelectorRowView : Grid
{
    private readonly SelectorRowViewModel _viewModel;
    private readonly PanelHeaderViewModel? _header;
    private bool _compact;
    private bool _narrow;
    private readonly ColumnDefinition _frequentsColumn = Star();
    private readonly ShortcutTile _frequents;
    private readonly SymbolIcon _star = PanelChrome.Icon("star", IconBox.Small, ColorToken.Text);
    private readonly StyledText _frequentsText = PanelChrome.Text(TextStyleToken.Label);
    private readonly ShortcutTile _profile;
    private readonly SymbolIcon _profileIcon = PanelChrome.Icon(
        null,
        IconBox.Small,
        ColorToken.Text
    );
    private readonly StyledText _profileName = PanelChrome.Text(TextStyleToken.Label);
    private readonly Border _dot = PanelChrome.Mark(ColorToken.AccentInk);
    private readonly Border _suggestionDot = PanelChrome.Mark(ColorToken.Warn);
    private readonly SymbolIcon _caret = PanelChrome.Icon(null, IconBox.Small, ColorToken.Muted);
    private readonly AutoFixedButton _autoFixed;

    /// <summary>Creates the row.</summary>
    /// <param name="viewModel">★ Frequents and the profile button.</param>
    /// <param name="header">The header, whose Auto/Fixed button the row shows; null leaves it out.</param>
    /// <param name="compact">The row of the Compact view (✕ closes the open profile grid).</param>
    public SelectorRowView(
        SelectorRowViewModel viewModel,
        PanelHeaderViewModel? header = null,
        bool compact = false
    )
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _header = header;
        _compact = compact;
        Height = ControlHeight.Medium;

        _frequents = Segment(ShortcutTilePattern.Invoke);
        _star.Margin = new Thickness(0, 0, Space.S2, 0);
        _frequentsText.VerticalAlignment = VerticalAlignment.Center;
        var star = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_star, Dock.Left);
        star.Children.Add(_star);
        star.Children.Add(_frequentsText);
        _frequents.Tag = star;
        _frequents.HorizontalContentAlignment = HorizontalAlignment.Center;
        _frequents.Invoked += (_, _) => _viewModel.Frequents();

        _profile = Segment(ShortcutTilePattern.ExpandCollapse);
        _profileIcon.Margin = new Thickness(0, 0, Space.S2, 0);
        _profileName.VerticalAlignment = VerticalAlignment.Center;
        _dot.Margin = new Thickness(Space.S1, 0, 0, 0);
        _suggestionDot.Margin = new Thickness(Space.S1, 0, 0, 0);
        _caret.Margin = new Thickness(Space.S1, 0, 0, 0);
        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_profileIcon, Dock.Left);
        DockPanel.SetDock(_caret, Dock.Right);
        DockPanel.SetDock(_suggestionDot, Dock.Right);
        DockPanel.SetDock(_dot, Dock.Right);
        content.Children.Add(_profileIcon);
        content.Children.Add(_caret);
        content.Children.Add(_suggestionDot);
        content.Children.Add(_dot);
        content.Children.Add(_profileName);
        _profile.Tag = content;
        _profile.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _profile.Invoked += (_, _) => _viewModel.ProfileButton();
        _profile.ExpandRequested += (_, _) => _viewModel.ProfileButton();
        _profile.CollapseRequested += (_, _) => _viewModel.ProfileButton();

        var segments = new Grid();
        segments.ColumnDefinitions.Add(_frequentsColumn);
        segments.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Space.S1) });
        segments.ColumnDefinitions.Add(Star());
        SetColumn(_profile, 2);
        segments.Children.Add(_frequents);
        segments.Children.Add(_profile);
        // The line of the track is drawn over it, inside, without taking room: the segments of 44 keep their whole
        // height in the track of 52 (S1 above and below), so the ring of the chosen one is never cut (D12).
        var fill = new Border
        {
            CornerRadius = new CornerRadius(Radii.Medium),
            Padding = new Thickness(Space.S1),
            Child = segments,
        };
        fill.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Side));
        var line = new Border
        {
            CornerRadius = new CornerRadius(Radii.Medium),
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };
        line.SetResourceReference(Border.BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        line.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        var track = new Grid();
        track.Children.Add(fill);
        track.Children.Add(line);

        _autoFixed = new AutoFixedButton
        {
            Focusable = false,
            IsTabStop = false,
            Margin = new Thickness(Space.S2, 0, 0, 0),
            Visibility = header is null ? Visibility.Collapsed : Visibility.Visible,
        };
        _autoFixed.ToggleRequested += (_, _) => _header?.ToggleLock();

        ColumnDefinitions.Add(Star());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_autoFixed, 1);
        Children.Add(track);
        Children.Add(_autoFixed);

        _viewModel.PropertyChanged += OnChanged;
        if (_header is not null)
        {
            _header.PropertyChanged += OnChanged;
        }

        Refresh();
    }

    /// <summary>★ Frequents, the profile button and Auto/Fixed while the row is on screen.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (Visibility != Visibility.Visible)
            {
                yield break;
            }

            yield return new PanelTapTarget(_frequents, _viewModel.Frequents);
            yield return new PanelTapTarget(_profile, _viewModel.ProfileButton);
            if (_header is not null && _autoFixed.Visibility == Visibility.Visible)
            {
                yield return new PanelTapTarget(_autoFixed, _header.ToggleLock);
            }
        }
    }

    /// <summary>The ★ Frequents button.</summary>
    public ShortcutTile FrequentsButton => _frequents;

    /// <summary>The profile button.</summary>
    public ShortcutTile ProfileButton => _profile;

    /// <summary>The Auto/Fixed button (CAB-003).</summary>
    public AutoFixedButton AutoFixed => _autoFixed;

    /// <summary>Whether the row is the bottom row of the Compact view (✕ closes the open profile grid, VCO-002).</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            _compact = value;
            Refresh();
        }
    }

    /// <summary>
    /// Whether the row is in the narrowest panel (S): ★ Frequents keeps only its star, a square of 44 named
    /// «Frecuentes», and the profile takes the rest of the track, so its name is read instead of «W…».
    /// </summary>
    public bool IsNarrow
    {
        get => _narrow;
        set
        {
            if (_narrow == value)
            {
                return;
            }

            _narrow = value;
            _frequentsColumn.Width = value ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
            _frequents.Width = value ? ControlHeight.Small : double.NaN;
            _frequents.Padding = value ? new Thickness(0) : new Thickness(Space.S2, 0, Space.S2, 0);
            _frequentsText.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            _star.Margin = value ? new Thickness(0) : new Thickness(0, 0, Space.S2, 0);
        }
    }

    /// <summary>The yellow dot of a pending profile suggestion (SEL-005).</summary>
    public Border SuggestionMark => _suggestionDot;

    /// <summary>Stops following the view models.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        if (_header is not null)
        {
            _header.PropertyChanged -= OnChanged;
        }
    }

    private static ColumnDefinition Star() =>
        new() { Width = new GridLength(1, GridUnitType.Star) };

    private static ShortcutTile Segment(ShortcutTilePattern pattern)
    {
        var segment = PanelChrome.NewButton(PanelChrome.Segment, pattern);
        segment.Height = ControlHeight.Small;
        segment.Padding = new Thickness(Space.S2, 0, Space.S2, 0);
        return segment;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        _frequents.AccessibleName = _viewModel.FrequentsName;
        _frequents.AccessibleState = _viewModel.FrequentsState;
        _frequentsText.Text = _viewModel.FrequentsName;
        Choose(_frequents, _star, _frequentsText, _viewModel.IsFrequentsActive);

        _profile.AccessibleName = _viewModel.ProfileName;
        _profile.AccessibleHelpText = _viewModel.ButtonHelp;
        _profile.AccessibleState = _viewModel.ButtonState;
        _profile.IsExpanded = _viewModel.IsExpanded;
        _profileName.Text = _viewModel.ProfileName;
        _profileIcon.Symbol = _viewModel.ProfileIcon.Length == 0 ? null : _viewModel.ProfileIcon;
        _caret.Symbol = _viewModel.Caret switch
        {
            SelectorCaret.Collapse => _compact ? "close" : "expand_less",
            SelectorCaret.Return => "undo",
            _ => "expand_more",
        };
        _dot.Visibility = _viewModel.IsActiveApp ? Visibility.Visible : Visibility.Collapsed;
        _suggestionDot.Visibility = _viewModel.HasSuggestion
            ? Visibility.Visible
            : Visibility.Collapsed;
        Choose(_profile, _profileIcon, _profileName, _viewModel.Look != SelectorLook.Frequents);

        if (_header is { } header)
        {
            // While the search has text, Auto/Fixed keeps its place under the search layer (CAB-001).
            _autoFixed.Visibility = header.ShowsAutoFixed ? Visibility.Visible : Visibility.Hidden;
            _autoFixed.IsChecked = header.IsFixed;
            _autoFixed.Symbol = header.AutoFixedIcon;
            _autoFixed.Label = header.AutoFixedLabel;
            _autoFixed.ToolTip = header.AutoFixedName;
            AutomationProperties.SetName(_autoFixed, header.AutoFixedName);
        }
    }

    /// <summary>The single look of «elegido» on a segment: wash, ring, the icon filled in <c>accentInk</c>.</summary>
    private static void Choose(ShortcutTile segment, SymbolIcon icon, StyledText text, bool chosen)
    {
        segment.ToggleState = chosen ? ToggleState.On : ToggleState.Off;
        icon.IsFilled = chosen;
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(chosen ? ColorToken.AccentInk : ColorToken.Text)
        );
        text.Ink = ColorToken.Text;
    }
}
