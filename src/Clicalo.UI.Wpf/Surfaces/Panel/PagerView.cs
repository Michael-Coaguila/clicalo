using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// ◀, the page dots and ▶ under the grid (CUA-004, VCO-002) in direction C (D12): a row of
/// <see cref="ControlHeight.Small"/> with secondary icon buttons of 44 × 44 at its ends, which look disabled on the first
/// and the last page, and the dots between them, the active one a pill of <see cref="Space.S6"/> in <c>accent</c> and
/// the others dots of <see cref="Space.S2"/> in <c>line</c>, each one named «Página n» inside a 44 × 44 target. The
/// panel reserves the row in its design, so a profile with one page does not make the panel shorter; then the row is
/// empty. It only projects <see cref="PagerViewModel"/>.
/// </summary>
public sealed class PagerView : DockPanel
{
    private readonly PagerViewModel _viewModel;
    private readonly IconButton _previous;
    private readonly IconButton _next;
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
        Height = ControlHeight.Small;
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

    /// <summary>Whether the pages are shown: more than one page in a view that pages (the panel decides).</summary>
    public bool ShowsPages { get; private set; }

    /// <summary>The visible tappable elements: the arrows and every dot.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!ShowsPages)
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

    /// <summary>◀.</summary>
    public IconButton PreviousButton => _previous;

    /// <summary>▶.</summary>
    public IconButton NextButton => _next;

    /// <summary>Shows the pages or leaves the row empty, keeping its place.</summary>
    /// <param name="shown">Whether the pages show.</param>
    public void ShowPages(bool shown)
    {
        ShowsPages = shown;
        var visibility = shown ? Visibility.Visible : Visibility.Hidden;
        _previous.Visibility = visibility;
        _next.Visibility = visibility;
        _dots.Visibility = visibility;
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private static IconButton Arrow(string symbol, Action action)
    {
        var arrow = new IconButton
        {
            Symbol = symbol,
            Variant = ButtonVariant.Secondary,
            Focusable = false,
            IsTabStop = false,
        };
        arrow.Click += (_, _) => action();
        return arrow;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        AutomationProperties.SetName(_previous, _viewModel.PreviousName);
        AutomationProperties.SetName(_next, _viewModel.NextName);
        // CUA-004: the arrows look disabled on the first and the last page; UI Automation hears it as not enabled.
        _previous.IsEnabled = _viewModel.CanGoPrevious;
        _next.IsEnabled = _viewModel.CanGoNext;
        _dots.Children.Clear();
        _dotControls.Clear();
        foreach (var dot in _viewModel.Dots)
        {
            var control = PanelChrome.NewButton(PanelChrome.Dot, ShortcutTilePattern.Invoke);
            control.Width = dot.WidthPx;
            control.Height = Space.S2;
            control.AccessibleName = dot.AccessibleName;
            control.AccessibleState = dot.AccessibleState;
            PanelChrome.SetBrush(
                control,
                Control.BackgroundProperty,
                dot.IsActive ? ColorToken.Accent : ColorToken.Line
            );
            var page = dot;
            control.Invoked += (_, _) => page.Select();
            _dots.Children.Add(control);
            _dotControls.Add((control, dot));
        }
    }
}
