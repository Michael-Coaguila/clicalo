using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The bottom row of the Compact view (VCO-002, docs/04 «Vista compacta»): ★ (44 × 40), ◀ (36 × 44) and ▶ with the page
/// dots between them when there are several pages, and the profile button (40 high, from 44 to 40 % of the width) with
/// its icon, its name of 12 and ▾, ✕ while its grid is open or ↶ in Frequents. It shows the same selector and pager as
/// the Full view (<see cref="SelectorRowViewModel"/>, <see cref="PagerViewModel"/>): from Frequents the profile button
/// goes back in one tap; otherwise it opens the profile grid above the row.
/// </summary>
public sealed class CompactRowView : DockPanel
{
    private const double StarWidth = 44;
    private const double RowHeight = 40;
    private const double ArrowWidth = 36;
    private const double IconPx = 20;
    private const double NamePx = 12;
    private const double DotHeight = 8;
    private const double Gap = 6;
    private const double ProfileShare = 0.4;

    private readonly SelectorRowViewModel _selector;
    private readonly PagerViewModel _pager;
    private readonly Func<bool> _emptyProfile;
    private readonly ShortcutTile _frequents;
    private readonly ShortcutTile _previous;
    private readonly ShortcutTile _next;
    private readonly ShortcutTile _profile;
    private readonly SymbolIcon _profileIcon = new()
    {
        Size = IconPx,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _profileName = new()
    {
        FontWeight = FontWeights.Bold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 2, 0),
    };

    private readonly SymbolIcon _caret = new()
    {
        Size = IconPx,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly StackPanel _dots = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly List<(ShortcutTile Control, PageDotViewModel Dot)> _dotControls = [];
    private bool _shown;

    /// <summary>Creates the row.</summary>
    /// <param name="selector">★ and the profile button.</param>
    /// <param name="pager">◀, the dots and ▶.</param>
    /// <param name="emptyProfile">Whether the empty profile card is on show instead of the grid.</param>
    public CompactRowView(
        SelectorRowViewModel selector,
        PagerViewModel pager,
        Func<bool> emptyProfile
    )
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(pager);
        ArgumentNullException.ThrowIfNull(emptyProfile);
        _selector = selector;
        _pager = pager;
        _emptyProfile = emptyProfile;
        Margin = new Thickness(12, Gap, 12, 0);
        LastChildFill = true;
        Visibility = Visibility.Collapsed;

        _frequents = PanelChrome.NewButton(PanelChrome.Large, ShortcutTilePattern.Invoke);
        _frequents.Width = StarWidth;
        _frequents.Height = RowHeight;
        _frequents.HorizontalContentAlignment = HorizontalAlignment.Center;
        _frequents.VerticalContentAlignment = VerticalAlignment.Center;
        _frequents.Tag = new SymbolIcon { Symbol = "star", Size = IconPx };
        _frequents.Invoked += (_, _) => _selector.Frequents();

        _previous = Arrow("chevron_left", _pager.Previous);
        _next = Arrow("chevron_right", _pager.Next);

        _profile = PanelChrome.NewButton(PanelChrome.Large, ShortcutTilePattern.ExpandCollapse);
        _profile.Height = RowHeight;
        _profile.MinWidth = TouchTarget.MinimumSize;
        _profile.Padding = new Thickness(8, 0, 6, 0);
        _profile.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _profile.VerticalContentAlignment = VerticalAlignment.Center;
        _profileName.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(NamePx));
        var content = new DockPanel { LastChildFill = true };
        SetDock(_profileIcon, Dock.Left);
        SetDock(_caret, Dock.Right);
        content.Children.Add(_profileIcon);
        content.Children.Add(_caret);
        content.Children.Add(_profileName);
        _profile.Tag = content;
        _profile.Invoked += (_, _) => _selector.ProfileButton();
        _profile.ExpandRequested += (_, _) => _selector.ProfileButton();
        _profile.CollapseRequested += (_, _) => _selector.ProfileButton();

        SetDock(_frequents, Dock.Left);
        SetDock(_profile, Dock.Right);
        SetDock(_previous, Dock.Left);
        SetDock(_next, Dock.Right);
        _previous.Margin = new Thickness(Gap, 0, 0, 0);
        _next.Margin = new Thickness(0, 0, Gap, 0);
        Children.Add(_frequents);
        Children.Add(_profile);
        Children.Add(_previous);
        Children.Add(_next);
        Children.Add(_dots);

        _selector.PropertyChanged += OnChanged;
        _pager.PropertyChanged += OnChanged;
        SizeChanged += (_, _) =>
            _profile.MaxWidth = Math.Max(TouchTarget.MinimumSize, ActualWidth * ProfileShare);
        Refresh();
    }

    /// <summary>★, the arrows, the dots and the profile button as targets of the panel's pointer layer.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!_shown)
            {
                yield break;
            }

            yield return new PanelTapTarget(_frequents, _selector.Frequents);
            yield return new PanelTapTarget(_profile, _selector.ProfileButton);
            if (_previous.Visibility == Visibility.Visible)
            {
                yield return new PanelTapTarget(_previous, _pager.Previous);
                yield return new PanelTapTarget(_next, _pager.Next);
                foreach (var (control, dot) in _dotControls)
                {
                    yield return new PanelTapTarget(control, dot.Select);
                }
            }
        }
    }

    /// <summary>★.</summary>
    public ShortcutTile FrequentsButton => _frequents;

    /// <summary>The profile button.</summary>
    public ShortcutTile ProfileButton => _profile;

    /// <summary>Shows or hides the row (VCO-002: Compact view, no search text).</summary>
    /// <param name="shown">Whether it shows.</param>
    public void Show(bool shown)
    {
        _shown = shown;
        Refresh();
    }

    /// <summary>Stops following the view models (when the surface closes).</summary>
    public void Detach()
    {
        _selector.PropertyChanged -= OnChanged;
        _pager.PropertyChanged -= OnChanged;
    }

    private static ShortcutTile Arrow(string symbol, Action action)
    {
        var arrow = PanelChrome.NewButton(PanelChrome.Button, ShortcutTilePattern.Invoke);
        arrow.Width = ArrowWidth;
        arrow.Height = TouchTarget.MinimumSize;
        arrow.HorizontalContentAlignment = HorizontalAlignment.Center;
        arrow.VerticalContentAlignment = VerticalAlignment.Center;
        arrow.Tag = new SymbolIcon { Symbol = symbol, Size = IconPx };
        PanelChrome.Paint(arrow, null, ColorToken.Text, null);
        arrow.Invoked += (_, _) => action();
        return arrow;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _shown ? Visibility.Visible : Visibility.Collapsed;
        _frequents.AccessibleName = _selector.FrequentsName;
        _frequents.AccessibleState = _selector.FrequentsState;
        if (_selector.IsFrequentsActive)
        {
            PanelChrome.Paint(_frequents, ColorToken.Accent, ColorToken.OnAccent, null);
        }
        else
        {
            PanelChrome.Paint(_frequents, null, ColorToken.Text, ColorToken.Border);
        }

        _profile.AccessibleName = _selector.ProfileName;
        _profile.AccessibleHelpText = _selector.ButtonHelp;
        _profile.AccessibleState = _selector.IsActiveApp ? _selector.ActiveAppName : string.Empty;
        _profile.IsExpanded = _selector.IsExpanded;
        _profileName.Text = _selector.ProfileName;
        _profileIcon.Symbol = _selector.ProfileIcon.Length == 0 ? null : _selector.ProfileIcon;
        _caret.Symbol = _selector.Caret switch
        {
            SelectorCaret.Collapse => "close",
            SelectorCaret.Return => "undo",
            _ => "expand_more",
        };
        switch (_selector.Look)
        {
            case SelectorLook.Active:
                PanelChrome.Paint(_profile, ColorToken.Accent, ColorToken.OnAccent, null);
                break;
            case SelectorLook.Open:
                PanelChrome.Paint(_profile, ColorToken.CardHi, ColorToken.Text, ColorToken.Border);
                break;
            default:
                PanelChrome.Paint(_profile, ColorToken.Card, ColorToken.Text, ColorToken.Border);
                break;
        }

        var paged = CompactRowRules.ShowsPager(_pager.PageCount, _emptyProfile());
        _previous.Visibility = SurfaceParts.Shown(paged);
        _next.Visibility = SurfaceParts.Shown(paged);
        _dots.Visibility = SurfaceParts.Shown(paged);
        _previous.AccessibleName = _pager.PreviousName;
        _next.AccessibleName = _pager.NextName;
        _previous.IsEnabled = _pager.CanGoPrevious;
        _next.IsEnabled = _pager.CanGoNext;
        _previous.SetResourceReference(
            Control.ForegroundProperty,
            ThemeBrushKey.For(_pager.CanGoPrevious ? ColorToken.Text : ColorToken.Line)
        );
        _next.SetResourceReference(
            Control.ForegroundProperty,
            ThemeBrushKey.For(_pager.CanGoNext ? ColorToken.Text : ColorToken.Line)
        );
        _dots.Children.Clear();
        _dotControls.Clear();
        foreach (var dot in _pager.Dots)
        {
            var control = PanelChrome.NewButton(PanelChrome.Dot, ShortcutTilePattern.Invoke);
            control.Width = dot.WidthPx;
            control.Height = DotHeight;
            // The 44 × 44 target of each dot overflows its drawing around its center (REG-02): the row keeps its measure.
            var overflowX = Math.Max(0, (TouchTarget.MinimumSize - dot.WidthPx) / 2);
            var overflowY = (TouchTarget.MinimumSize - DotHeight) / 2;
            control.Margin = new Thickness(2 - overflowX, -overflowY, 2 - overflowX, -overflowY);
            control.AccessibleName = dot.AccessibleName;
            control.AccessibleState = dot.AccessibleState;
            PanelChrome.Paint(
                control,
                dot.IsActive ? ColorToken.Accent : ColorToken.Line,
                ColorToken.Text,
                null
            );
            var page = dot;
            control.Invoked += (_, _) => page.Select();
            _dots.Children.Add(control);
            _dotControls.Add((control, dot));
        }
    }
}
