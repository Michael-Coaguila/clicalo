using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// ◀, the page dots and ▶ under the grid (CUA-004): the arrows are 48 × 40 cards that dim on the first and last page;
/// the active dot is 24 × 10 in accent and the others 10 × 10 in line; each dot is named «Página n» and has a 44 touch
/// target. It only projects <see cref="PagerViewModel"/>.
/// </summary>
public sealed class PagerView : DockPanel
{
    private const double ArrowWidth = 48;
    private const double ArrowHeight = 40;
    private const double ArrowIcon = 24;
    private const double DotHeight = 10;
    private const double Gap = 8;

    private readonly PagerViewModel _viewModel;
    private readonly ShortcutTile _previous;
    private readonly ShortcutTile _next;
    private readonly StackPanel _dots = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly List<(ShortcutTile Control, PageDotViewModel Dot)> _dotControls = [];

    /// <summary>Creates the pager of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The pager.</param>
    public PagerView(PagerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Margin = new Thickness(12, Gap, 12, 0);
        LastChildFill = true;
        _previous = Arrow("chevron_left", viewModel.Previous);
        _next = Arrow("chevron_right", viewModel.Next);
        SetDock(_previous, Dock.Left);
        SetDock(_next, Dock.Right);
        Children.Add(_previous);
        Children.Add(_next);
        Children.Add(_dots);
        viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>The visible tappable elements: the arrows and every dot.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!_viewModel.IsVisible)
            {
                yield break;
            }

            yield return new PanelTapTarget(_previous, _viewModel.Previous);
            yield return new PanelTapTarget(_next, _viewModel.Next);
            foreach (var (control, dot) in _dotControls)
            {
                yield return new PanelTapTarget(control, dot.Select);
            }
        }
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private static ShortcutTile Arrow(string symbol, Action action)
    {
        var arrow = PanelChrome.NewButton(PanelChrome.Button, ShortcutTilePattern.Invoke);
        arrow.Width = ArrowWidth;
        arrow.Height = ArrowHeight;
        arrow.HorizontalContentAlignment = HorizontalAlignment.Center;
        arrow.VerticalContentAlignment = VerticalAlignment.Center;
        arrow.Tag = new SymbolIcon { Symbol = symbol, Size = ArrowIcon };
        PanelChrome.Paint(arrow, ColorToken.Card, ColorToken.Text, ColorToken.Border);
        arrow.Invoked += (_, _) => action();
        return arrow;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _previous.AccessibleName = _viewModel.PreviousName;
        _next.AccessibleName = _viewModel.NextName;
        // CUA-004: the arrows dim on the first and the last page; UI Automation hears it as not enabled, not by color alone.
        _previous.IsEnabled = _viewModel.CanGoPrevious;
        _next.IsEnabled = _viewModel.CanGoNext;
        _previous.SetResourceReference(
            Control.ForegroundProperty,
            ThemeBrushKey.For(_viewModel.CanGoPrevious ? ColorToken.Text : ColorToken.Line)
        );
        _next.SetResourceReference(
            Control.ForegroundProperty,
            ThemeBrushKey.For(_viewModel.CanGoNext ? ColorToken.Text : ColorToken.Line)
        );
        _dots.Children.Clear();
        _dotControls.Clear();
        foreach (var dot in _viewModel.Dots)
        {
            var control = PanelChrome.NewButton(PanelChrome.Dot, ShortcutTilePattern.Invoke);
            control.Width = dot.WidthPx;
            control.Height = DotHeight;
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
