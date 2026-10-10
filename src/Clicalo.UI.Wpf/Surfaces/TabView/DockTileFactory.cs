using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// Turns a <see cref="DockTileViewModel"/> into a <see cref="ShortcutTile"/> without keys nor badges (PES-007, PES-010):
/// icon, name, voice number and accessible state, kept in step with the view model until <see cref="Detach"/>. With
/// the modes of the bar, UI Automation Invoke asks test mode first (PES-014), a right click or the accessible secondary
/// action opens the menu of the shortcut (CUA-014, PES-010), and the tile shows the ✓ or ⊘ of test mode (TAC-008).
/// </summary>
internal static class DockTileFactory
{
    /// <summary>Creates the tile of <paramref name="viewModel"/>.</summary>
    public static (ShortcutTile Control, PropertyChangedEventHandler Handler) Create(
        DockTileViewModel viewModel,
        double width,
        double height,
        double iconPx,
        double labelPx,
        DockTileModes? modes = null
    )
    {
        var control = new ShortcutTile
        {
            Template = ShortcutTileTemplate.Default,
            Focusable = false,
            IsTabStop = false,
            Pattern =
                viewModel.Behavior == TileBehavior.Tap
                    ? ShortcutTilePattern.Invoke
                    : ShortcutTilePattern.Toggle,
            Width = width,
            Height = height,
            Margin = new Thickness(0),
            Padding = new Thickness(4),
            IconSize = iconPx,
        };
        control.SetResourceReference(
            Control.FontSizeProperty,
            ThemeKeys.ScaledTextSize(Math.Max(TypeScale.Minimum, labelPx))
        );
        control.Invoked += (_, _) => Invoke(viewModel, modes);
        control.Toggled += (_, _) => Invoke(viewModel, modes);
        if (modes is not null)
        {
            control.SecondaryRequested += (_, _) => _ = modes.OpenMenu(viewModel);
        }

        PropertyChangedEventHandler handler = (_, _) => Paint(control, viewModel);
        viewModel.PropertyChanged += handler;
        Paint(control, viewModel);
        return (control, handler);
    }

    /// <summary>
    /// The tile with the ✓ or ⊘ of test mode over it (PES-014, TAC-008); the tile itself without the modes. The margin of
    /// the tile goes on what this returns.
    /// </summary>
    /// <param name="control">The tile.</param>
    /// <param name="viewModel">Its shortcut.</param>
    /// <param name="modes">The modes of the bar, or <see langword="null"/>.</param>
    public static (FrameworkElement Cell, TestMarkBadge? Badge) Cell(
        ShortcutTile control,
        DockTileViewModel viewModel,
        DockTileModes? modes
    )
    {
        if (modes is null)
        {
            return (control, null);
        }

        var badge = new TestMarkBadge(modes.TestMode, viewModel.Id);
        var cell = new Grid();
        cell.Children.Add(control);
        cell.Children.Add(badge);
        return (cell, badge);
    }

    /// <summary>Stops following the view model.</summary>
    public static void Detach(DockTileViewModel viewModel, PropertyChangedEventHandler handler) =>
        viewModel.PropertyChanged -= handler;

    private static void Invoke(DockTileViewModel viewModel, DockTileModes? modes)
    {
        if (modes?.Tapped(viewModel) != true)
        {
            viewModel.Invoke();
        }
    }

    private static void Paint(ShortcutTile control, DockTileViewModel viewModel)
    {
        control.Keys = string.Empty;
        control.Badge = string.Empty;
        control.AccessibleName = viewModel.AccessibleName;
        control.AccessibleState = viewModel.AccessibleState;
        control.AccessibleHelpText = viewModel.AccessibleHelpText;
        control.VoiceNumber = viewModel.VoiceNumber;
        control.ToggleState = viewModel.IsLatched ? ToggleState.On : ToggleState.Off;
        control.Symbol = viewModel.Icon.Length == 0 ? null : viewModel.Icon;
        control.Category = TileFactory.CategoryOf(viewModel.Category);
        control.IsHeld = viewModel.IsLatched && viewModel.Behavior == TileBehavior.Hold;
    }
}
