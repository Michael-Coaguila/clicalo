using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.General;

/// <summary>
/// The miniatures of «General y panel» (GEN-006, GEN-008, GEN-010): every one is the same box of
/// <see cref="ControlHeight.Large"/> × <see cref="ControlHeight.Small"/> on the surface with its line, so the options
/// that carry them line up. What they draw is in <c>muted</c> lines, and in the accent while their option is chosen.
/// They are pictures: UI Automation reads the name of their option.
/// </summary>
internal static class Miniatures
{
    /// <summary>
    /// «Filas visibles»: <paramref name="rows"/> rows of tiles from the top. Auto draws its default rows with the sign
    /// of what adapts by itself beside them, so it never draws the same picture as the option of its number.
    /// </summary>
    public static Border Rows(int rows, bool selected, bool auto = false)
    {
        var stack = new StackPanel();
        for (var i = 0; i < rows; i++)
        {
            var bar = Cell(selected);
            bar.Height = Space.S2;
            bar.Margin = new Thickness(0, i == 0 ? 0 : Space.S1, 0, 0);
            stack.Children.Add(bar);
        }

        if (!auto)
        {
            return Box(stack);
        }

        var sign = Ui.Icon(
            "autorenew",
            IconBox.Badge,
            selected ? ColorToken.AccentInk : ColorToken.Muted
        );
        sign.HorizontalAlignment = HorizontalAlignment.Right;
        sign.VerticalAlignment = VerticalAlignment.Center;
        stack.Margin = new Thickness(0, 0, IconBox.Badge + Space.S1, 0);
        var layers = new Grid();
        layers.Children.Add(stack);
        layers.Children.Add(sign);
        return Box(layers);
    }

    /// <summary>«Columnas»: two rows of <paramref name="columns"/> tiles.</summary>
    public static Border Columns(int columns, bool selected)
    {
        var grid = new UniformGrid { Columns = Math.Max(1, columns), Rows = 2 };
        for (var i = 0; i < columns * 2; i++)
        {
            var cell = Cell(selected);
            cell.Margin = new Thickness(Space.S1 / 2);
            grid.Children.Add(cell);
        }

        return Box(grid);
    }

    /// <summary>«Mostrar teclas»: a tile with its name and, when <paramref name="keys"/> is set, its keys.</summary>
    public static Border Tile(string name, string? keys)
    {
        var column = Ui.Column(
            0,
            Centered(SettingsUi.Styled(name, TextStyleToken.LabelSmall)),
            keys is null
                ? null
                : Centered(SettingsUi.Styled(keys, TextStyleToken.KeysTile, ColorToken.Muted))
        );
        column.VerticalAlignment = VerticalAlignment.Center;
        var tile = new Border
        {
            Child = column,
            CornerRadius = new CornerRadius(Radii.Small),
            BorderThickness = new Thickness(Stroke.Line),
        };
        Ui.Ink(tile, Border.BackgroundProperty, ColorToken.Card);
        Ui.Ink(tile, Border.BorderBrushProperty, ColorToken.Border);
        return Box(tile, padding: 0);
    }

    /// <summary>
    /// «Lado»: the bar on its edge of the screen and the scroll bar on the right, which the bar leaves free with
    /// [gutter] (GEN-010).
    /// </summary>
    public static Border Side(DockSide side, bool gutter, bool selected)
    {
        var area = new Grid { ClipToBounds = true };
        var scroll = new Border
        {
            Width = Space.S1,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Ui.Ink(scroll, Border.BackgroundProperty, ColorToken.Border);
        area.Children.Add(scroll);
        var bar = Cell(selected);
        switch (side)
        {
            case DockSide.Left:
                bar.Width = Space.S2;
                bar.HorizontalAlignment = HorizontalAlignment.Left;
                bar.Margin = new Thickness(0, Space.S2, 0, Space.S2);
                break;
            case DockSide.Top:
                bar.Height = Space.S2;
                bar.VerticalAlignment = VerticalAlignment.Top;
                bar.Margin = new Thickness(Space.S3, 0, Space.S3, 0);
                break;
            case DockSide.Bottom:
                bar.Height = Space.S2;
                bar.VerticalAlignment = VerticalAlignment.Bottom;
                bar.Margin = new Thickness(Space.S3, 0, Space.S3, 0);
                break;
            default:
                bar.Width = Space.S2;
                bar.HorizontalAlignment = HorizontalAlignment.Right;
                bar.Margin = new Thickness(0, Space.S2, gutter ? Space.S2 : 0, Space.S2);
                break;
        }

        area.Children.Add(bar);
        return Box(area);
    }

    private static Border Cell(bool selected)
    {
        var cell = new Border { CornerRadius = new CornerRadius(Space.S1 / 2) };
        Ui.Ink(cell, Border.BackgroundProperty, selected ? ColorToken.Accent : ColorToken.Line);
        return cell;
    }

    private static Border Box(UIElement child, double padding = Space.S1)
    {
        var box = new Border
        {
            Child = child,
            Width = ControlHeight.Large,
            Height = ControlHeight.Small,
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(Radii.Small),
            BorderThickness = new Thickness(Stroke.Line),
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false,
        };
        Ui.Ink(box, Border.BackgroundProperty, ColorToken.Win);
        Ui.Ink(box, Border.BorderBrushProperty, ColorToken.Border);
        return box;
    }

    private static T Centered<T>(T element)
        where T : FrameworkElement
    {
        element.HorizontalAlignment = HorizontalAlignment.Center;
        return element;
    }
}
