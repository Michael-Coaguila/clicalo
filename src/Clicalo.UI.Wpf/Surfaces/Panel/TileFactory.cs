using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
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
    public static readonly DependencyProperty HidesTypeProperty =
        DependencyProperty.RegisterAttached(
            "HidesType",
            typeof(bool),
            typeof(TileFactory),
            new PropertyMetadata(false)
        );

    /// <summary>The anatomy <see cref="Size"/> gave a tile, so it is placed again when what it shows changes.</summary>
    private static readonly DependencyProperty LookProperty = DependencyProperty.RegisterAttached(
        "Look",
        typeof(TileLook),
        typeof(TileFactory),
        new PropertyMetadata(null)
    );

    /// <summary>Whether a tile already places its body when its voice number or its width change.</summary>
    private static readonly DependencyProperty PlacesProperty = DependencyProperty.RegisterAttached(
        "Places",
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
                viewModel.Behavior == TileBehavior.Tap || viewModel.ScrollsWhileHeld
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
    public static void ShowName(ShortcutTile control, bool visible)
    {
        ShowPart(control, ShortcutTileTemplate.LabelPart, visible);

        // Without a name under it, the icon keeps no gap for one: it sits in the middle of the tile.
        if (Part(control, ShortcutTileTemplate.IconPart) is { } icon)
        {
            if (visible)
            {
                icon.ClearValue(FrameworkElement.MarginProperty);
            }
            else
            {
                icon.Margin = new Thickness(0);
            }
        }
    }

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
            look.LeadInset ?? look.SideInset,
            look.TopInset,
            look.SideInset,
            Bottom(look)
        );
        control.IconSize = look.IconSize;
        ShowPart(control, ShortcutTileTemplate.IconPart, look.ShowsIcon);
        // Without its icon the name rises to the height of the corner: the type mark there would sit on it, so it only
        // stays in the help of the tile («Mantener»). Beside a centred number it shows while the pair leaves it room.
        ShowPart(control, ShortcutTileTemplate.TypePart, look.ShowsIcon && !look.CentresNumber);
        var number = Part(control, ShortcutTileTemplate.VoiceNumberPart);
        if (number is not null)
        {
            if (look.CentresNumber)
            {
                number.VerticalAlignment = VerticalAlignment.Center;
                number.Margin = new Thickness(Space.S1, 0, 0, 0);
            }
            else
            {
                number.ClearValue(FrameworkElement.VerticalAlignmentProperty);
                number.ClearValue(FrameworkElement.MarginProperty);
            }
        }

        // Around a number in its corner the name and the origin are placed across the whole tile, not in the box of
        // the widest of them.
        if (Part(control, ShortcutTileTemplate.BodyPart) is { } body)
        {
            if (look.ClearsCornerNumber)
            {
                body.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            else
            {
                body.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
            }
        }

        Part(control, ShortcutTileTemplate.LabelPart)?.ClearValue(FrameworkElement.MarginProperty);
        if (Part(control, ShortcutTileTemplate.KeysPart) is TextBlock keys)
        {
            keys.ClearValue(FrameworkElement.HorizontalAlignmentProperty);

            // A profile of origin is words, not keys: the interface font, as the state in words that may take its place,
            // across the whole inner width of the tile (BUS-005, FRE-003).
            if (look.KeysAreWords)
            {
                keys.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.UiFont);
                keys.FontWeight = FontWeights.Bold;
                keys.Margin = WordsMargin(look, 0);
            }
            else
            {
                keys.ClearValue(TextBlock.FontFamilyProperty);
                keys.ClearValue(TextBlock.FontWeightProperty);
                keys.ClearValue(FrameworkElement.MarginProperty);
            }
        }

        control.MaxNameLines = look.NameLines;

        control.SetResourceReference(
            Control.FontSizeProperty,
            ThemeKeys.FontSize(look.Name, scaled: true)
        );
        control.SetResourceReference(
            ShortcutTile.KeysFontSizeProperty,
            ThemeKeys.FontSize(look.Keys, scaled: true)
        );

        control.SetValue(LookProperty, look);
        if (
            (look.CentresNumber || look.ClearsCornerNumber)
            && number is not null
            && !(bool)control.GetValue(PlacesProperty)
        )
        {
            // The number is as wide as its digits: the body is placed once it is measured, and again when it grows.
            control.SetValue(PlacesProperty, true);
            SizeChangedEventHandler place = (_, _) => Place(control);
            number.SizeChanged += place;
            control.SizeChanged += place;
        }
    }

    /// <summary>
    /// Places the body of a tile around its voice number, when its look says so: beside a number centred down the tile
    /// (<see cref="TileLook.CentresNumber"/>), or around a number in its corner (<see cref="TileLook.ClearsCornerNumber"/>).
    /// </summary>
    private static void Place(ShortcutTile control)
    {
        if (
            control.GetValue(LookProperty) is not TileLook look
            || Part(control, ShortcutTileTemplate.VoiceNumberPart) is not { } number
        )
        {
            return;
        }

        if (look.CentresNumber)
        {
            PlaceBesideNumber(control, number, look);
        }
        else if (look.ClearsCornerNumber)
        {
            PlaceAroundCornerNumber(control, number, look);
        }
    }

    /// <summary>
    /// Puts the body of a tile beside its voice number, which is centred down the tile (<see cref="TileLook.CentresNumber"/>),
    /// whatever the digits of the number: the body starts <see cref="Space.S1"/> after it, so a number of two digits
    /// never covers the icon or the name. An icon alone makes one pair with the number, centred across the tile; when
    /// the tile has a type mark («Mantener» of Hablar) and the pair fits whole beside its corner, the mark stays and the
    /// pair moves only as far as it must to keep clear of it.
    /// </summary>
    private static void PlaceBesideNumber(
        ShortcutTile control,
        FrameworkElement number,
        TileLook look
    )
    {
        var badge = number.ActualWidth;
        var width = control.ActualWidth;
        if (badge <= 0 || width <= 0)
        {
            return;
        }

        var pair = badge + Space.S1 + look.IconSize;
        var markLeft = width - Space.S1 - IconBox.Badge;
        var mark = look.ShowsIcon && control.TypeSymbol is not null && Space.S1 + pair <= markLeft;
        ShowPart(control, ShortcutTileTemplate.TypePart, mark);
        var centred = (width - pair) / 2;
        var side =
            !look.ShowsIcon ? Space.S1
            : mark ? Math.Max(Space.S1, Math.Min(centred, markLeft - Space.S1 - pair))
            : Math.Max(Space.S1, centred);
        number.Margin = new Thickness(side, 0, 0, 0);
        control.Padding = new Thickness(
            side + badge + Space.S1,
            look.TopInset,
            look.ShowsIcon ? width - side - pair : look.SideInset,
            Bottom(look)
        );
    }

    /// <summary>
    /// Places the name and the origin of a tile without an icon around its voice number, which keeps its corner at the
    /// top left as on every tile (<see cref="TileLook.ClearsCornerNumber"/>): under it when the two lines fit there,
    /// both centred in the tile; otherwise the name goes beside it, centred in the room it leaves and ending in «…» only
    /// when it does not fit there, and the origin keeps the whole inner width under them, centred with the name while it
    /// fits that room and reaching under the number when it is longer. Without a number both are centred in the tile.
    /// </summary>
    private static void PlaceAroundCornerNumber(
        ShortcutTile control,
        FrameworkElement number,
        TileLook look
    )
    {
        var width = control.ActualWidth;
        var height = control.ActualHeight;
        if (
            width <= 0
            || height <= 0
            || Part(control, ShortcutTileTemplate.LabelPart) is not TextBlock label
            || Part(control, ShortcutTileTemplate.KeysPart) is not TextBlock keys
        )
        {
            return;
        }

        var shown = number.Visibility == Visibility.Visible && number.ActualWidth > 0;
        var hasKeys = keys.Visibility == Visibility.Visible;
        var corner = Space.S1 + number.ActualHeight;
        var lines = LineOf(label) + (hasKeys ? Space.S1 + LineOf(keys) : 0);
        var under = shown && corner + lines + Space.S1 <= height;
        var lead = shown && !under ? Space.S1 + number.ActualWidth + Space.S1 : look.SideInset;
        control.Padding = new Thickness(
            look.SideInset,
            under ? corner : look.TopInset,
            look.SideInset,
            Bottom(look)
        );
        label.Margin = new Thickness(lead - look.SideInset, 0, 0, 0);
        if (!shown || !hasKeys)
        {
            keys.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
            keys.Margin = WordsMargin(look, 0);
            return;
        }

        // The origin is centred under the name while it fits, and slides left, under the number, when it is longer.
        var centre = (lead + width - look.SideInset) / 2;
        var origin = Math.Min(width - (2 * Space.S1), Math.Ceiling(NaturalWidth(keys)));
        var left = Math.Clamp(centre - (origin / 2), Space.S1, width - Space.S1 - origin);
        keys.HorizontalAlignment = HorizontalAlignment.Left;
        keys.Margin = WordsMargin(look, left - Space.S1);
    }

    /// <summary>
    /// The margin of a line of words under the name: <see cref="Space.S1"/> under the name and the whole inner width of
    /// the tile, <see cref="Space.S1"/> inside its edge as the slots of its corners, past the side inset of the body;
    /// <paramref name="lead"/> moves its left edge further in.
    /// </summary>
    private static Thickness WordsMargin(TileLook look, double lead)
    {
        var reach = look.SideInset - Space.S1;
        return new Thickness(lead - reach, Space.S1, -reach, 0);
    }

    /// <summary>The height of one line of <paramref name="text"/>.</summary>
    private static double LineOf(TextBlock text) =>
        double.IsNaN(text.LineHeight)
            ? text.FontFamily.LineSpacing * text.FontSize
            : text.LineHeight;

    /// <summary>The width <paramref name="text"/> takes whole, in one line.</summary>
    private static double NaturalWidth(TextBlock text) =>
        new FormattedText(
            text.Text,
            CultureInfo.CurrentCulture,
            text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
            text.FontSize,
            text.Foreground,
            VisualTreeHelper.GetDpi(text).PixelsPerDip
        ).WidthIncludingTrailingWhitespace;

    /// <summary>The space under the body: none when the body already starts under the slot of the voice number.</summary>
    private static double Bottom(TileLook look) => look.TopInset > Space.S1 ? 0 : Space.S1;

    private static FrameworkElement? Part(ShortcutTile control, string part)
    {
        control.ApplyTemplate();
        return control.Template?.FindName(part, control) as FrameworkElement;
    }

    private static void ShowPart(ShortcutTile control, string part, bool visible)
    {
        if (Part(control, part) is { } element)
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
        control.OriginSymbol = viewModel.KeyLineIsOrigin ? viewModel.OriginIcon : null;
        control.Category = CategoryOf(viewModel.Category);
        var hidesType = (bool)control.GetValue(HidesTypeProperty);
        control.Badge = hidesType ? string.Empty : viewModel.Badge;
        control.TypeSymbol = hidesType ? null : viewModel.TypeSymbol;
        control.IsFlashing = viewModel.IsFlashing;

        // EJE-002: the armed tile keeps a warn outline until its confirmation tap or the end of its window.
        control.IsArmed = viewModel.IsArmed;

        // CUA-009: a Mantener tile held down shrinks with its ring; a latched toggle shows its state and its wash.
        control.IsHeld = viewModel.IsLatched && viewModel.Behavior == TileBehavior.Hold;

        // A state in words in the place of the origin, or a type mark that comes or goes, moves what is placed around
        // the voice number.
        Place(control);
    }
}
