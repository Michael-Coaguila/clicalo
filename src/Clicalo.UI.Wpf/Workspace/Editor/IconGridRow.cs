using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Editor;

/// <summary>
/// A row of the <see cref="IconGrid"/>: its cells, reused for another row as the grid scrolls. A cell is an option of
/// the width the grid gives it, <see cref="IconGrid.Cell"/> high, with the icon centered in its box of
/// <see cref="IconBox.Medium"/>. It is named with the hand-written words of the icon in the language of the interface
/// («copiar, duplicar»), or with its Material Symbols name with spaces when it has none (REG-06); a name another cell
/// shares has that Material name as help. The icon in use is drawn «elegido» and filled.
/// </summary>
internal sealed class IconGridRow : StackPanel
{
    private readonly IconGrid _grid;

    /// <summary>Creates a row of <paramref name="grid"/>.</summary>
    /// <param name="grid">The grid it belongs to.</param>
    public IconGridRow(IconGrid grid)
    {
        _grid = grid;
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(0, 0, 0, IconGrid.Gap);
    }

    /// <summary>Shows <paramref name="cells"/>, with <paramref name="current"/> marked.</summary>
    /// <param name="cells">The icons of the row and their names.</param>
    /// <param name="current">The icon in use.</param>
    public void Show(IconGrid.RowCells cells, string current)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var icons = cells.Icons;
        while (Children.Count < icons.Length)
        {
            Children.Add(NewCell());
        }

        for (var i = 0; i < Children.Count; i++)
        {
            var cell = (OptionButton)Children[i];
            if (i >= icons.Length)
            {
                cell.Visibility = Visibility.Collapsed;
                continue;
            }

            var icon = icons[i];
            cell.Visibility = Visibility.Visible;
            cell.Width = _grid.CellWidth;
            cell.Tag = icon;
            ((SymbolIcon)cell.Content).Symbol = icon;
            AutomationProperties.SetName(cell, cells.Names[i]);
            AutomationProperties.SetHelpText(cell, cells.Helps[i]);
            Mark(cell, current);
        }
    }

    /// <summary>Gives the cells the width the grid has for them now.</summary>
    public void Resize()
    {
        foreach (OptionButton cell in Children)
        {
            cell.Width = _grid.CellWidth;
        }
    }

    /// <summary>Marks the cell of <paramref name="current"/> and only it.</summary>
    /// <param name="current">The icon in use.</param>
    public void Mark(string current)
    {
        foreach (OptionButton cell in Children)
        {
            Mark(cell, current);
        }
    }

    private static void Mark(OptionButton cell, string current)
    {
        var on = string.Equals(cell.Tag as string, current, StringComparison.Ordinal);
        cell.IsSelected = on;
        var icon = (SymbolIcon)cell.Content;
        icon.IsFilled = on;
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(on ? ColorToken.AccentInk : ColorToken.Text)
        );
    }

    private OptionButton NewCell()
    {
        var cell = new OptionButton
        {
            Kind = OptionKind.Tile,
            Content = new SymbolIcon
            {
                Size = IconBox.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = _grid.CellWidth,
            Height = IconGrid.Cell,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, IconGrid.Gap, 0),
        };
        cell.Click += (_, _) =>
        {
            if (cell.Tag is string icon)
            {
                _grid.OnPicked(icon);
            }
        };
        return cell;
    }
}
