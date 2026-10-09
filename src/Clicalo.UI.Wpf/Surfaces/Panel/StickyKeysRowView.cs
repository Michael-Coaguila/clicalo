using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Domain.StickyModifiers;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The sticky modifiers row (FIJ-005): Ctrl · Alt · Shift · Win in 13 px monospace, 40 high (44 touch target), 4 equal
/// columns. Released: card with a border; once: accent; locked: accent and 🔒. Each key is a three-state Toggle for UI
/// Automation (Off, On = once, Indeterminate = locked), with its state in words.
/// </summary>
public sealed class StickyKeysRowView : UniformGrid
{
    private const double Gap = 6;
    private const double KeyHeight = 40;
    private const double KeyPx = 13;
    private const double LockIcon = 13;

    private readonly StickyKeysRowViewModel _viewModel;
    private readonly List<(StickyKeyViewModel Key, ShortcutTile Control, SymbolIcon Lock)> _keys =
    [];

    /// <summary>Creates the row of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The row.</param>
    public StickyKeysRowView(StickyKeysRowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Columns = viewModel.Keys.Count;
        Rows = 1;
        Margin = new Thickness(12 - (Gap / 2), 0, 12 - (Gap / 2), 10 - (Gap / 2));
        foreach (var key in viewModel.Keys)
        {
            var control = PanelChrome.NewButton(PanelChrome.Button, ShortcutTilePattern.Toggle);
            control.Height = KeyHeight;
            control.Margin = new Thickness(Gap / 2);
            control.HorizontalContentAlignment = HorizontalAlignment.Center;
            control.VerticalContentAlignment = VerticalAlignment.Center;
            control.AccessibleName = key.Label;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var label = new TextBlock
            {
                Text = key.Label,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
            label.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(KeyPx));
            var padlock = new SymbolIcon
            {
                Symbol = "lock",
                Size = LockIcon,
                Margin = new Thickness(3, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            content.Children.Add(label);
            content.Children.Add(padlock);
            control.Tag = content;
            var tapped = key;
            control.Toggled += (_, _) => tapped.Tap();
            control.Invoked += (_, _) => tapped.Tap();
            key.PropertyChanged += OnKeyChanged;
            _keys.Add((key, control, padlock));
            Children.Add(control);
        }

        viewModel.PropertyChanged += OnRowChanged;
        Refresh();
    }

    /// <summary>The four keys while the row shows.</summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        _viewModel.IsVisible
            ? _keys.Select(static k => new PanelTapTarget(k.Control, k.Key.Tap))
            : [];

    /// <summary>The control of each key, in order (tests locate them).</summary>
    public IReadOnlyList<ShortcutTile> KeyControls => [.. _keys.Select(static k => k.Control)];

    /// <summary>Stops following the view model.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnRowChanged;
        foreach (var (key, _, _) in _keys)
        {
            key.PropertyChanged -= OnKeyChanged;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnKeyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(this, _viewModel.AccessibleName);
        foreach (var (key, control, padlock) in _keys)
        {
            control.AccessibleState = key.AccessibleState;
            control.ToggleState = key.Level switch
            {
                StickyLevel.Once => ToggleState.On,
                StickyLevel.Locked => ToggleState.Indeterminate,
                _ => ToggleState.Off,
            };
            padlock.Visibility = key.IsLocked ? Visibility.Visible : Visibility.Collapsed;
            if (key.IsActive)
            {
                PanelChrome.Paint(
                    control,
                    ColorToken.Accent,
                    ColorToken.OnAccent,
                    ColorToken.Accent
                );
            }
            else
            {
                PanelChrome.Paint(control, ColorToken.Card, ColorToken.Text, ColorToken.Border);
            }
        }
    }
}
