using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// Builds and paints the <see cref="ShortcutTile"/> of a <see cref="TileViewModel"/> for the grid and the Always
/// visible row: the pattern of its behavior, its names, state, icon, category, badge and voice number (ACC-009: the
/// yellow number at the top left, and «{n} {name}» as its UI Automation name).
/// </summary>
internal static class TileFactory
{
    /// <summary>A new tile for <paramref name="viewModel"/>, kept painted while it lives.</summary>
    /// <param name="viewModel">The tile's view model.</param>
    /// <param name="intercept">
    /// Asked first on a UI Automation Invoke or Toggle (edit mode and test mode take it, EJE-001); when it returns
    /// <see langword="true"/> the tile does not run.
    /// </param>
    /// <returns>The control and the handler to detach with <see cref="Detach"/>.</returns>
    public static (ShortcutTile Control, PropertyChangedEventHandler Handler) Create(
        TileViewModel viewModel,
        Func<TileViewModel, bool>? intercept = null
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
        };
        control.Invoked += (_, _) => Invoke(viewModel, intercept);
        control.Toggled += (_, _) => Invoke(viewModel, intercept);
        PropertyChangedEventHandler handler = (_, _) => Paint(control, viewModel);
        viewModel.PropertyChanged += handler;
        Paint(control, viewModel);
        return (control, handler);
    }

    /// <summary>Stops painting a tile.</summary>
    /// <param name="viewModel">The tile's view model.</param>
    /// <param name="handler">The handler <see cref="Create"/> returned.</param>
    public static void Detach(TileViewModel viewModel, PropertyChangedEventHandler handler) =>
        viewModel.PropertyChanged -= handler;

    /// <summary>Hides the name of a tile (the Always visible row in S and Compact, FIJ-002); its UI Automation name stays.</summary>
    /// <param name="control">The tile.</param>
    /// <param name="visible">Whether the name shows.</param>
    public static void ShowName(ShortcutTile control, bool visible)
    {
        control.ApplyTemplate();
        if (
            control.Template?.FindName(ShortcutTileTemplate.LabelPart, control)
            is FrameworkElement label
        )
        {
            label.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>The color category of a persisted category id (TEM-003); an unknown one falls back to Edit.</summary>
    /// <param name="category">The category id.</param>
    public static CategoryToken CategoryOf(string category) =>
        Enum.TryParse<CategoryToken>(category, ignoreCase: true, out var token)
        && Enum.IsDefined(token)
            ? token
            : CategoryToken.Edit;

    /// <summary>Gives the tile its sizes: tile box, gap, icon and the scaled name (CUA-011).</summary>
    /// <param name="control">The tile.</param>
    /// <param name="height">Its height.</param>
    /// <param name="gap">The gap around it.</param>
    /// <param name="iconSize">Its icon.</param>
    /// <param name="labelPx">Its name, before the text scale; at least 11 (TEM-007).</param>
    /// <param name="keysFontSize">Its key line, already scaled (CUA-011); 0 keeps the default size.</param>
    public static void Size(
        ShortcutTile control,
        double height,
        double gap,
        double iconSize,
        double labelPx,
        double keysFontSize = 0
    )
    {
        if (keysFontSize > 0)
        {
            control.KeysFontSize = keysFontSize;
        }

        control.Height = height;
        control.Margin = new Thickness(gap / 2);
        control.Padding = new Thickness(gap / 2);
        control.IconSize = iconSize;
        control.SetResourceReference(
            Control.FontSizeProperty,
            ThemeKeys.ScaledTextSize(Math.Max(TypeScale.Minimum, labelPx))
        );
    }

    private static void Invoke(TileViewModel viewModel, Func<TileViewModel, bool>? intercept)
    {
        if (intercept?.Invoke(viewModel) != true)
        {
            viewModel.Invoke();
        }
    }

    private static void Paint(ShortcutTile control, TileViewModel viewModel)
    {
        control.Keys = viewModel.Keys;
        control.AccessibleName = viewModel.AccessibleName;
        control.AccessibleState = viewModel.AccessibleState;
        control.AccessibleHelpText = viewModel.AccessibleHelpText;
        control.VoiceNumber = viewModel.VoiceNumber;
        control.ToggleState = viewModel.IsLatched ? ToggleState.On : ToggleState.Off;
        control.Symbol = viewModel.Icon.Length == 0 ? null : viewModel.Icon;
        control.Category = CategoryOf(viewModel.Category);
        control.Badge = viewModel.Badge;
        control.IsFlashing = viewModel.IsFlashing;

        // CUA-009: a Mantener tile held down shrinks with its outline; a latched toggle shows ACTIVO and its wash.
        control.IsHeld = viewModel.IsLatched && viewModel.Behavior == TileBehavior.Hold;
    }
}
