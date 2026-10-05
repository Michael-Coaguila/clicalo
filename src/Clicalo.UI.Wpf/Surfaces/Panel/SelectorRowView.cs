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
/// The profile selector of the Full view (SEL-001): two columns of the same width and 44 high with a gap of 6. ★
/// Frequents is filled with accent while Frequents is in view and outlined otherwise. The profile button shows the icon
/// (20), the name (15, bold, cut with «…»), the 8 px dot of the active app and ▾, ▴ or ↶; it is accent while its
/// profile is in view, cardHi while the profile grid is open and card with a border in Frequents, and it is an
/// ExpandCollapse for UI Automation. It only projects <see cref="SelectorRowViewModel"/>.
/// </summary>
public sealed class SelectorRowView : Grid
{
    private const double Gap = 6;
    private const double Height44 = 44;
    private const double IconPx = 20;
    private const double FrequentsPx = 14;
    private const double NamePx = 15;
    private const double DotPx = 8;

    private readonly SelectorRowViewModel _viewModel;
    private readonly ShortcutTile _frequents;
    private readonly TextBlock _frequentsText = new()
    {
        FontWeight = FontWeights.Bold,
        VerticalAlignment = VerticalAlignment.Center,
    };

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
        Margin = new Thickness(Gap, 0, Gap, 0),
    };

    private readonly Border _dot = new()
    {
        Width = DotPx,
        Height = DotPx,
        CornerRadius = new CornerRadius(DotPx / 2),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, Gap, 0),
    };

    private readonly SymbolIcon _caret = new()
    {
        Size = IconPx,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Creates the selector of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The selector.</param>
    public SelectorRowView(SelectorRowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Margin = new Thickness(12, 0, 12, 10);
        ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Gap) });
        ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );

        _frequents = PanelChrome.NewButton(PanelChrome.Large, ShortcutTilePattern.Invoke);
        _frequents.Height = Height44;
        _frequents.HorizontalContentAlignment = HorizontalAlignment.Center;
        _frequents.VerticalContentAlignment = VerticalAlignment.Center;
        var star = new StackPanel { Orientation = Orientation.Horizontal };
        star.Children.Add(
            new SymbolIcon
            {
                Symbol = "star",
                Size = IconPx,
                Margin = new Thickness(0, 0, Gap, 0),
                VerticalAlignment = VerticalAlignment.Center,
            }
        );
        _frequentsText.SetResourceReference(
            TextBlock.FontSizeProperty,
            ThemeKeys.TextSize(FrequentsPx)
        );
        star.Children.Add(_frequentsText);
        _frequents.Tag = star;
        _frequents.Invoked += (_, _) => _viewModel.Frequents();
        SetColumn(_frequents, 0);
        Children.Add(_frequents);

        _profile = PanelChrome.NewButton(PanelChrome.Large, ShortcutTilePattern.ExpandCollapse);
        _profile.Height = Height44;
        _profile.Padding = new Thickness(10, 0, 8, 0);
        _profile.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _profile.VerticalContentAlignment = VerticalAlignment.Center;
        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_profileIcon, Dock.Left);
        DockPanel.SetDock(_caret, Dock.Right);
        DockPanel.SetDock(_dot, Dock.Right);
        content.Children.Add(_profileIcon);
        content.Children.Add(_caret);
        content.Children.Add(_dot);
        _profileName.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(NamePx));
        content.Children.Add(_profileName);
        _profile.Tag = content;
        _profile.Invoked += (_, _) => _viewModel.ProfileButton();
        _profile.ExpandRequested += (_, _) => _viewModel.ProfileButton();
        _profile.CollapseRequested += (_, _) => _viewModel.ProfileButton();
        SetColumn(_profile, 2);
        Children.Add(_profile);

        viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>★ Frequents and the profile button while the row shows.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!_viewModel.IsVisible)
            {
                yield break;
            }

            yield return new PanelTapTarget(_frequents, _viewModel.Frequents);
            yield return new PanelTapTarget(_profile, _viewModel.ProfileButton);
        }
    }

    /// <summary>The ★ Frequents button.</summary>
    public ShortcutTile FrequentsButton => _frequents;

    /// <summary>The profile button.</summary>
    public ShortcutTile ProfileButton => _profile;

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _frequents.AccessibleName = _viewModel.FrequentsName;
        _frequents.AccessibleState = _viewModel.FrequentsState;
        _frequentsText.Text = _viewModel.FrequentsName;
        if (_viewModel.IsFrequentsActive)
        {
            PanelChrome.Paint(_frequents, ColorToken.Accent, ColorToken.OnAccent, null);
        }
        else
        {
            PanelChrome.Paint(_frequents, null, ColorToken.Text, ColorToken.Border);
        }

        _profile.AccessibleName = _viewModel.ProfileName;
        _profile.AccessibleHelpText = _viewModel.ButtonHelp;
        _profile.AccessibleState = _viewModel.IsActiveApp ? _viewModel.ActiveAppName : string.Empty;
        _profile.IsExpanded = _viewModel.IsExpanded;
        _profileName.Text = _viewModel.ProfileName;
        _profileIcon.Symbol = _viewModel.ProfileIcon.Length == 0 ? null : _viewModel.ProfileIcon;
        _caret.Symbol = _viewModel.Caret switch
        {
            SelectorCaret.Collapse => "expand_less",
            SelectorCaret.Return => "undo",
            _ => "expand_more",
        };
        _dot.Visibility = _viewModel.IsActiveApp ? Visibility.Visible : Visibility.Collapsed;
        var active = _viewModel.Look == SelectorLook.Active;
        PanelChrome.SetBrush(
            _dot,
            Border.BackgroundProperty,
            active ? ColorToken.OnAccent : ColorToken.Accent
        );
        switch (_viewModel.Look)
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
    }
}
