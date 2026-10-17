using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Shortcuts;

/// <summary>
/// The tiles of the grid of «Atajos» (ATJ-009), drawn as the tiles of the panel (D12, «Así se ven en el panel»): a card
/// of <see cref="TileHeight"/> with the icon in the tint of its category, the name in one line with «…» and the line of
/// keys, and reserved slots that never cover the icon: the voice number (or the sign of a repeated combination) at the
/// top left and the kind at the top right. The tile in the editor has the single look of «elegido» and its icon filled.
/// </summary>
internal static class GridTiles
{
    /// <summary>The height of a tile: two touch targets.</summary>
    public const double TileHeight = ControlHeight.Small * 2;

    /// <summary>The least width of a tile in the grid.</summary>
    public const double MinWidth = TileHeight + Space.S4;

    /// <summary>How far the corner slots sit from the edge: inside the ring of «elegido».</summary>
    private const double SlotInset = Space.S1 + Stroke.Selected;

    /// <summary>The tile of <paramref name="tile"/>; the view adds the tap and the drag.</summary>
    /// <param name="tile">The shortcut.</param>
    /// <param name="incompleteText">[incomplete].</param>
    public static OptionButton Tile(GridTile tile, string incompleteText)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var icon = Ui.CategoryIcon(tile.Icon, IconBox.Medium, tile.Category);
        icon.IsFilled = tile.Selected;
        var name = new StyledText(tile.Name, TextStyleToken.Label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, Space.S1, 0, 0),
        };
        var foot = tile.Incomplete
            ? new StyledText(incompleteText, TextStyleToken.LabelSmall, ColorToken.WarnText)
            : new StyledText(tile.Foot, TextStyleToken.KeysTile, ColorToken.Muted);
        foot.HorizontalAlignment = HorizontalAlignment.Center;
        foot.Margin = new Thickness(0, Space.S1, 0, 0);
        var center = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S2, 0, Space.S2, 0),
        };
        center.Children.Add(icon);
        center.Children.Add(name);
        if (tile.Incomplete || tile.Foot.Length > 0)
        {
            center.Children.Add(foot);
        }

        var layers = new Grid();
        layers.Children.Add(center);
        FrameworkElement? left =
            tile.Number is { } number ? new Badge { Text = number, Tone = BadgeTone.Warn }
            : tile.Repeated ? Ui.Icon("warning", IconBox.Badge, ColorToken.WarnText)
            : null;
        FrameworkElement? right =
            tile.Number is not null && tile.Repeated
                ? Ui.Icon("warning", IconBox.Badge, ColorToken.WarnText)
            : tile.TypeIcon is { } type ? Ui.Icon(type, IconBox.Badge, ColorToken.Muted)
            : null;
        if (left is not null)
        {
            layers.Children.Add(Slot(left, HorizontalAlignment.Left));
        }

        if (right is not null)
        {
            layers.Children.Add(Slot(right, HorizontalAlignment.Right));
        }

        var button = new OptionButton
        {
            Kind = OptionKind.Tile,
            Content = layers,
            IsSelected = tile.Selected,
            Height = TileHeight,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Tag = tile.Id,
        };
        AutomationProperties.SetName(button, tile.AccessibleName);
        AutomationProperties.SetItemStatus(button, tile.AccessibleState);
        return button;
    }

    /// <summary>
    /// The tile «Biblioteca» at the end of the grid: an action, not a shortcut, so it has no card, only a dashed line,
    /// and its icon in the ink of the accent.
    /// </summary>
    /// <param name="text">[fromLib].</param>
    /// <param name="open">Opens «Añadir atajo».</param>
    public static TouchButton Library(string text, Action open)
    {
        ArgumentNullException.ThrowIfNull(open);
        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(Ui.Icon("library_add", IconBox.Medium, ColorToken.AccentInk));
        content.Children.Add(
            new StyledText(text, TextStyleToken.Label)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(Space.S2, Space.S1, Space.S2, 0),
            }
        );
        var button = new TouchButton
        {
            Content = Dashed(content, TileHeight),
            Variant = ButtonVariant.Tertiary,
            Height = TileHeight,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => open();
        return button;
    }

    /// <summary>
    /// A dashed line of the control limit around <paramref name="content"/>, with the radius of a tile. The button
    /// centers its content, so the line takes the height of the button itself, never the height of what it holds.
    /// </summary>
    /// <param name="content">What goes inside.</param>
    /// <param name="height">The height of the button that holds it.</param>
    public static Grid Dashed(UIElement content, double height)
    {
        var frame = new Rectangle
        {
            RadiusX = Radii.Medium,
            RadiusY = Radii.Medium,
            StrokeThickness = Stroke.Line,
            StrokeDashArray = new DoubleCollection { Space.S1, Space.S1 },
            IsHitTestVisible = false,
        };
        Ui.Ink(frame, Shape.StrokeProperty, ColorToken.Line);
        var layers = new Grid { Height = height };
        layers.Children.Add(frame);
        layers.Children.Add(content);
        return layers;
    }

    private static FrameworkElement Slot(FrameworkElement element, HorizontalAlignment side)
    {
        element.HorizontalAlignment = side;
        element.VerticalAlignment = VerticalAlignment.Top;
        element.Margin = new Thickness(SlotInset);
        element.IsHitTestVisible = false;
        return element;
    }
}
