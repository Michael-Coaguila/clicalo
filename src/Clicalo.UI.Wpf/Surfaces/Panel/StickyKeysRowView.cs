using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Domain.StickyModifiers;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The sticky modifiers row (FIJ-005) of direction C (D12): Ctrl · Alt · Shift · Win in four equal columns with the gap
/// of the grid, keys of <see cref="ControlHeight.Small"/> drawn as controls (the card with its line) and their names in
/// the <c>keys</c> style, the only use of the monospace. Released: at rest; once: the single look of «elegido»;
/// locked: the accent fill with a lock. Each key is a three-state Toggle for UI Automation (Off, On = once,
/// Indeterminate = locked), with its state in words.
/// </summary>
public sealed class StickyKeysRowView : UniformGrid
{
    private readonly StickyKeysRowViewModel _viewModel;
    private readonly List<(
        StickyKeyViewModel Key,
        ShortcutTile Control,
        StyledText Label,
        SymbolIcon Lock
    )> _keys = [];

    private bool _keepsPlace;
    private bool _covered;

    /// <summary>Creates the row of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The row.</param>
    public StickyKeysRowView(StickyKeysRowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Columns = viewModel.Keys.Count;
        Margin = new Thickness(-Space.S1);
        foreach (var key in viewModel.Keys)
        {
            var control = PanelChrome.NewButton(PanelChrome.Key, ShortcutTilePattern.Toggle);
            control.Height = ControlHeight.Small;
            control.Margin = new Thickness(Space.S1);
            control.AccessibleName = key.Label;
            var label = new StyledText(key.Label, TextStyleToken.Keys)
            {
                MaxLines = 1,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var padlock = PanelChrome.Icon("lock", IconBox.Badge, ColorToken.OnAccent);
            padlock.Margin = new Thickness(Space.S1, 0, 0, 0);
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(label);
            content.Children.Add(padlock);
            control.Tag = content;
            var tapped = key;
            control.Toggled += (_, _) => tapped.Tap();
            control.Invoked += (_, _) => tapped.Tap();
            key.PropertyChanged += OnKeyChanged;
            _keys.Add((key, control, label, padlock));
            Children.Add(control);
        }

        viewModel.PropertyChanged += OnRowChanged;
        Refresh();
    }

    /// <summary>The four keys while the row shows.</summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        _viewModel.IsVisible && !_covered
            ? _keys.Select(static k => new PanelTapTarget(k.Control, k.Key.Tap))
            : [];

    /// <summary>The control of each key, in order (tests locate them).</summary>
    public IReadOnlyList<ShortcutTile> KeyControls => [.. _keys.Select(static k => k.Control)];

    /// <summary>
    /// Whether the row keeps its place while it is hidden: the search with text hides it (PAN-008) under the search
    /// layer, and the grid does not move (direction C, D12).
    /// </summary>
    public bool KeepsPlace
    {
        get => _keepsPlace;
        set
        {
            _keepsPlace = value;
            Refresh();
        }
    }

    /// <summary>
    /// Whether a layer of the panel covers the row (the search): it keeps its place but is not on screen, so UI
    /// Automation and the finger do not reach it under the layer.
    /// </summary>
    public bool IsCovered
    {
        get => _covered;
        set
        {
            _covered = value;
            Refresh();
        }
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnRowChanged;
        foreach (var (key, _, _, _) in _keys)
        {
            key.PropertyChanged -= OnKeyChanged;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnKeyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility =
            _viewModel.IsVisible ? (_covered ? Visibility.Hidden : Visibility.Visible)
            : _keepsPlace ? Visibility.Hidden
            : Visibility.Collapsed;
        AutomationProperties.SetName(this, _viewModel.AccessibleName);
        foreach (var (key, control, label, padlock) in _keys)
        {
            control.AccessibleState = key.AccessibleState;
            control.ToggleState = key.Level switch
            {
                StickyLevel.Once => ToggleState.On,
                StickyLevel.Locked => ToggleState.Indeterminate,
                _ => ToggleState.Off,
            };
            padlock.Visibility = key.IsLocked ? Visibility.Visible : Visibility.Collapsed;
            label.Ink = key.IsLocked ? ColorToken.OnAccent : ColorToken.Text;
        }
    }
}
