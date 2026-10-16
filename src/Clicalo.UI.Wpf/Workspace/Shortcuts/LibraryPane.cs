using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Shortcuts;

/// <summary>
/// «Añadir atajo» (ATJ-010), state A of the editor column: its title with ✕, «Crear el mío» as a row that goes to the
/// blank editor, the categories as chips and the ready actions as rows of 52 with their keys and ⊕ or ✓.
/// </summary>
internal static class LibraryPane
{
    /// <summary>The pane of <paramref name="model"/>.</summary>
    /// <param name="model">«Añadir atajo».</param>
    /// <param name="close">✕.</param>
    /// <param name="createOwn">«Crear el mío».</param>
    /// <param name="choose">A category chip.</param>
    /// <param name="add">A ready action, by its index.</param>
    public static StackPanel Build(
        LibraryModel model,
        Action close,
        Action createOwn,
        Action<string> choose,
        Action<int> add
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(createOwn);
        ArgumentNullException.ThrowIfNull(choose);
        ArgumentNullException.ThrowIfNull(add);
        var shut = new IconButton { Symbol = "close" };
        AutomationProperties.SetName(shut, model.CloseName);
        shut.Click += (_, _) => close();
        var head = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(shut, Dock.Right);
        head.Children.Add(shut);
        head.Children.Add(
            new StyledText(model.Title, TextStyleToken.Heading)
            {
                VerticalAlignment = VerticalAlignment.Center,
            }
        );

        var create = new NavRow
        {
            Content = model.CreateTitle,
            Caption = model.CreateText,
            Symbol = "edit_square",
        };
        AutomationProperties.SetName(create, model.CreateTitle);
        AutomationProperties.SetHelpText(create, model.CreateText);
        create.Click += (_, _) => createOwn();

        var chips = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
        foreach (var chip in model.Chips)
        {
            var option = new OptionButton
            {
                Kind = OptionKind.Chip,
                Content = chip.Label,
                Symbol = chip.Icon,
                IsSelected = chip.Selected,
                Margin = new Thickness(0, 0, Space.S2, Space.S2),
            };
            AutomationProperties.SetName(option, chip.Label);
            option.Click += (_, _) => choose(chip.Id);
            chips.Children.Add(option);
        }

        var rows = new StackPanel();
        foreach (var row in model.Rows)
        {
            rows.Children.Add(Row(row, add));
        }

        var column = new StackPanel();
        column.Children.Add(head);
        create.Margin = new Thickness(0, Space.S3, 0, Space.S6);
        column.Children.Add(create);
        column.Children.Add(
            new Overline(model.OrPick) { Margin = new Thickness(0, 0, 0, Space.S2) }
        );
        column.Children.Add(chips);
        rows.Margin = new Thickness(0, Space.S4, 0, 0);
        column.Children.Add(rows);
        return column;
    }

    /// <summary>A ready action: its icon in the tint of its category, name and keys, and ⊕ to add it or ✓ when it is in.</summary>
    private static TouchButton Row(LibraryRow row, Action<int> add)
    {
        var icon = Ui.CategoryIcon(row.Icon, IconBox.Medium, row.Category);
        icon.Margin = new Thickness(0, 0, Space.S3, 0);
        var state = Ui.Icon(
            row.Added ? "check_circle" : "add_circle",
            IconBox.Medium,
            row.Added ? ColorToken.Success : ColorToken.AccentInk
        );
        state.IsFilled = row.Added;
        state.Margin = new Thickness(Space.S2, 0, 0, 0);
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new StyledText(row.Name, TextStyleToken.Label));
        if (row.Foot.Length > 0)
        {
            lines.Children.Add(new StyledText(row.Foot, TextStyleToken.Keys, ColorToken.Muted));
        }

        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(state, Dock.Right);
        content.Children.Add(icon);
        content.Children.Add(state);
        content.Children.Add(lines);
        var button = new TouchButton
        {
            Content = content,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Medium,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S3, 0, Space.S3, 0),
            Margin = new Thickness(0, 0, 0, Space.S2),
        };
        AutomationProperties.SetName(button, row.Name);
        AutomationProperties.SetItemStatus(button, row.AccessibleState);
        button.Click += (_, _) => add(row.Index);
        return button;
    }
}
