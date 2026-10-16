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
/// visible row, with the anatomy of direction C (D12, CUA-007): the pattern of its behavior, its names, state, icon,
/// category, the type icon in the slot at the top right, the key line and the voice number in the slot at the top left
/// (ACC-009: «{n} {name}» as its UI Automation name). Nothing is drawn over the icon.
/// </summary>
internal static class TileFactory
{
    /// <summary>
    /// Identifies the attached flag that hides the type icon of a tile while edit mode puts its × in the same slot at
    /// the top right (CUA-012): the slots of a tile never overlap.
    /// </summary>
    public static readonly DependencyProperty HidesTypeProperty = DependencyProperty.RegisterAttached(
        "HidesType",
        typeof(bool),
        typeof(TileFactory),
        new PropertyMetadata(false)
    );

    /// <summary>A new tile for <paramref name="viewModel"/>, kept painted while it lives.</summary>
    /// <param name="viewModel">The tile's view model.</param>
    /// <param name="intercept">
    /// Asked first on a UI Automation Invoke or Toggle (edit mode and test mode take it, EJE-001); when it returns
    /// <see langword="true"/> the tile does not run.
    /// </param>
    /// <param name="secondary">The secondary action of the tile (right click, CUA-014): opens its menu.</param>
    /// <param name="showsKeys">Whether the key line shows (not in the Always visible row nor in Compact, VCO-001).</param>
    /// <returns>The control and the handler to detach with <see cref="Detach"/>.</returns>
    public static (ShortcutTile Control, PropertyChangedEventHandler Handler) Create(
        TileViewModel viewModel,
        Func<TileViewModel, bool>? intercept = null,
        Action<TileViewModel>? secondary = null,
        bool showsKeys = true
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
        control.SecondaryRequested += (_, _) => secondary?.Invoke(viewModel);
        PropertyChangedEventHandler handler = (_, _) => Paint(control, viewModel, showsKeys);
        viewModel.PropertyChanged += handler;
        Paint(control, viewModel, showsKeys);
        return (control, handler);
    }

    /// <summary>Stops painting a tile.</summary>
    /// <param name="viewModel">The tile's view model.</param>
    /// <param name="handler">The handler <see cref="Create"/> returned.</param>
    public static void Detach(TileViewModel viewModel, PropertyChangedEventHandler handler) =>
        viewModel.PropertyChanged -= handler;

    /// <summary>Shows or hides the name of a tile; its UI Automation name stays.</summary>
    /// <param name="control">The tile.</param>
    /// <param name="visible">Whether the name shows.</param>
    public static void ShowName(ShortcutTile control, bool visible) =>
        ShowPart(control, ShortcutTileTemplate.LabelPart, visible);

    /// <summary>Hides or shows again the type icon of a tile (its slot holds the × of edit mode).</summary>
    /// <param name="control">The tile.</param>
    /// <param name="viewModel">Its view model.</param>
    /// <param name="hide">Whether the slot is taken.</param>
    public static void HideType(ShortcutTile control, TileViewModel viewModel, bool hide)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(viewModel);
        control.SetValue(HidesTypeProperty, hide);
        control.TypeSymbol = hide ? null : viewModel.TypeSymbol;
        control.Badge = hide ? string.Empty : viewModel.Badge;
    }

    /// <summary>The color category of a persisted category id (TEM-003); an unknown one falls back to Edit.</summary>
    /// <param name="category">The category id.</param>
    public static CategoryToken CategoryOf(string category) =>
        Enum.TryParse<CategoryToken>(category, ignoreCase: true, out var token)
        && Enum.IsDefined(token)
            ? token
            : CategoryToken.Edit;

    /// <summary>
    /// Gives the tile its measures: its height, half the gap of the grid around it (<see cref="Space.S1"/>), its inner
    /// padding and the anatomy of <paramref name="look"/>; the name and the key line grow with the text scale (CUA-011).
    /// </summary>
    /// <param name="control">The tile.</param>
    /// <param name="height">Its height.</param>
    /// <param name="look">Its anatomy.</param>
    public static void Size(ShortcutTile control, double height, TileLook look)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(look);
        control.Height = height;
        control.Margin = new Thickness(Space.S1);
        control.Padding = new Thickness(
            Space.S2,
            look.TopInset,
            Space.S2,
            look.TopInset > Space.S1 ? 0 : Space.S1
        );
        control.IconSize = look.IconSize;
        ShowPart(control, ShortcutTileTemplate.IconPart, look.ShowsIcon);
        control.MaxNameLines = look.NameLines;
        control.SetResourceReference(
            Control.FontSizeProperty,
            ThemeKeys.FontSize(look.Name, scaled: true)
        );
        control.SetResourceReference(
            ShortcutTile.KeysFontSizeProperty,
            ThemeKeys.FontSize(look.Keys, scaled: true)
        );
    }

    private static void ShowPart(ShortcutTile control, string part, bool visible)
    {
        control.ApplyTemplate();
        if (control.Template?.FindName(part, control) is FrameworkElement element)
        {
            // Shown again, the part follows its template (an icon without a symbol stays collapsed).
            if (visible)
            {
                element.ClearValue(UIElement.VisibilityProperty);
            }
            else
            {
                element.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static void Invoke(TileViewModel viewModel, Func<TileViewModel, bool>? intercept)
    {
        if (intercept?.Invoke(viewModel) != true)
        {
            viewModel.Invoke();
        }
    }

    private static void Paint(ShortcutTile control, TileViewModel viewModel, bool showsKeys)
    {
        control.Keys = showsKeys ? viewModel.Keys : string.Empty;
        control.AccessibleName = viewModel.AccessibleName;
        control.AccessibleState = viewModel.AccessibleState;
        control.AccessibleHelpText = viewModel.AccessibleHelpText;
        control.VoiceNumber = viewModel.VoiceNumber;
        control.ToggleState = viewModel.IsLatched ? ToggleState.On : ToggleState.Off;
        control.Symbol = viewModel.Icon.Length == 0 ? null : viewModel.Icon;
        control.Category = CategoryOf(viewModel.Category);
        var hidesType = (bool)control.GetValue(HidesTypeProperty);
        control.Badge = hidesType ? string.Empty : viewModel.Badge;
        control.TypeSymbol = hidesType ? null : viewModel.TypeSymbol;
        control.IsFlashing = viewModel.IsFlashing;

        // EJE-002: the armed tile keeps a warn outline until its confirmation tap or the end of its window.
        control.IsArmed = viewModel.IsArmed;

        // CUA-009: a Mantener tile held down shrinks with its ring; a latched toggle shows its state and its wash.
        control.IsHeld = viewModel.IsLatched && viewModel.Behavior == TileBehavior.Hold;
    }
}
