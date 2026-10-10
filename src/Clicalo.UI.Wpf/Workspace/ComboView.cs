using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.Editor;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// The combination box with its key picker (docs/05 §1 B, EDI-007 to EDI-010): the keys in press order with their ×,
/// the count, backspace and Limpiar, the warning of a blocked or special combination, the modifiers, the groups, the
/// keys of the group and «Grabar con teclado». The editor of a shortcut and a row of the preview of Plantillas
/// (PLA-016) draw the same box, each with its own <see cref="IComboEditor"/>.
/// </summary>
internal static class ComboView
{
    /// <summary>Draws <paramref name="model"/>; every tap goes to <paramref name="editor"/>.</summary>
    /// <param name="model">The combination box.</param>
    /// <param name="editor">Who owns the combination.</param>
    public static StackPanel Build(ComboModel model, IComboEditor editor)
    {
        var box = Ui.Column(0);
        if (model.Replacing is { } replacing)
        {
            var keep = Ui.Button(
                Ui.Text(model.KeepOldText, 12, bold: true),
                model.KeepOldText,
                editor.KeepOldCombination,
                ColorToken.Card,
                stroke: ColorToken.Border
            );
            var strip = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(keep, Dock.Right);
            strip.Children.Add(keep);
            strip.Children.Add(
                Ui.Row(
                    8,
                    Ui.Icon("swap_horiz", 18, ColorToken.Warn),
                    Ui.Text(replacing, 12, wrap: true)
                )
            );
            box.Children.Add(
                Ui.Card(strip, ColorToken.WarnWash, null, 0, new Thickness(10, 6, 6, 6))
            );
        }

        var chips = new List<UIElement>();
        if (model.Recording is { } recording)
        {
            var listening = Ui.Text(recording, 14, ink: ColorToken.Accent, wrap: true);
            AutomationProperties.SetLiveSetting(listening, AutomationLiveSetting.Polite);
            chips.Add(listening);
        }

        if (model.Empty is { } empty)
        {
            chips.Add(Ui.Text(empty, 13, ink: ColorToken.Muted, wrap: true));
        }

        foreach (var chip in model.Chips)
        {
            if (chip.Index > 0)
            {
                chips.Add(Ui.Separator("+", 13));
            }

            var key = Ui.Button(
                Ui.Row(
                    4,
                    Ui.Text(chip.Label, 14, mono: true),
                    Ui.Icon("close", 16, ColorToken.Muted)
                ),
                chip.RemoveName,
                () => editor.RemoveKey(chip.Index),
                ColorToken.Card,
                stroke: ColorToken.Border,
                radius: 8
            );
            key.Padding = new Thickness(10, 0, 6, 0);
            chips.Add(key);
        }

        var keys = Ui.Wrap(4, chips);
        keys.Margin = new Thickness(8, 8, 4, 4);
        keys.MinHeight = 54;
        box.Children.Add(keys);
        var back = Ui.Button(Ui.Icon("backspace", 22), model.BackName, editor.RemoveLastKey);
        back.Width = 44;
        back.Padding = new Thickness(0);
        back.BorderThickness = new Thickness(0);
        back.IsEnabled = model.HasKeys;
        var clear = Ui.Button(
            Ui.IconLabel("restart_alt", model.ClearText, 18, 12),
            model.ClearText,
            editor.ClearKeys
        );
        clear.IsEnabled = model.HasKeys;
        clear.BorderThickness = new Thickness(0);
        var foot = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(clear, Dock.Right);
        DockPanel.SetDock(back, Dock.Right);
        foot.Children.Add(clear);
        foot.Children.Add(back);
        foot.Children.Add(Ui.Text(model.Count, 12, ink: ColorToken.Muted));
        box.Children.Add(Ui.Card(foot, ColorToken.Card, null, 0, new Thickness(12, 0, 4, 0)));
        var column = Ui.Column(
            8,
            Ui.Caption(model.Title),
            Ui.Card(
                box,
                model.Recording is null ? ColorToken.Field : ColorToken.AccentWash,
                model.Recording is null ? ColorToken.Border : ColorToken.Accent,
                12,
                new Thickness(0),
                model.Recording is null ? 1 : 2
            )
        );
        if (model.WarningText is { } warning)
        {
            var danger = model.Warning == WarningTone.Danger;
            var card = Ui.Card(
                Ui.Row(
                    8,
                    Ui.Icon(
                        danger ? "block" : "info",
                        20,
                        danger ? ColorToken.DangerText : ColorToken.Warn
                    ),
                    Ui.Text(warning, 13, wrap: true)
                ),
                danger ? ColorToken.DangerWash : ColorToken.WarnWash,
                danger ? ColorToken.Danger : ColorToken.Warn,
                10,
                new Thickness(12, 10, 12, 10)
            );
            AutomationProperties.SetLiveSetting(
                card,
                danger ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite
            );
            column.Children.Add(card);
        }

        if (model.StepEditing is { } step)
        {
            var done = Ui.Button(
                Ui.Text(model.DoneText, 12, bold: true, ink: ColorToken.OnAccent),
                model.DoneText,
                editor.StopEditingStep,
                ColorToken.Accent,
                ColorToken.OnAccent,
                radius: 8
            );
            var strip = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(done, Dock.Right);
            strip.Children.Add(done);
            strip.Children.Add(
                Ui.Row(8, Ui.Icon("edit", 18, ColorToken.Accent), Ui.Text(step, 12, bold: true))
            );
            column.Children.Add(
                Ui.Card(strip, ColorToken.AccentWash, null, 10, new Thickness(10, 4, 4, 4))
            );
        }

        // Four across, as in the prototype; in the narrow preview of a template they would be under 44, so the grid
        // wraps them (REG-02).
        var modifiers = new AutoFillGrid { Columns = 4, Gap = 6 };
        foreach (var cell in model.Modifiers)
        {
            modifiers.Children.Add(KeyButton(cell, editor));
        }

        column.Children.Add(modifiers);
        var groups = model.Groups.Select(group =>
        {
            var tab = Ui.Choice(
                Ui.Text(
                    group.Label,
                    12,
                    bold: true,
                    ink: group.Selected ? ColorToken.Text : ColorToken.Muted
                ),
                group.Label,
                group.Selected,
                () => editor.ChooseGroup(group.Group),
                44,
                8,
                offFill: null,
                role: CcToggleRole.Option
            );
            if (group.Selected)
            {
                CcChrome.Paint(tab, ColorToken.CardHi, ColorToken.Text, null);
            }
            else
            {
                CcChrome.Paint(tab, null, ColorToken.Muted, null);
            }

            tab.BorderThickness = new Thickness(0);
            tab.Padding = new Thickness(8, 0, 8, 0);
            return (UIElement)tab;
        });
        column.Children.Add(Ui.Wrap(4, groups));
        var cells = new AutoFillGrid
        {
            Columns = model.Columns,
            MinItemWidth = 92,
            Gap = 4,
        };
        foreach (var cell in model.Cells)
        {
            cells.Children.Add(KeyButton(cell, editor));
        }

        column.Children.Add(cells);
        column.Children.Add(
            Ui.Row(
                6,
                Ui.Icon("format_list_numbered", 16, ColorToken.Muted),
                Ui.Text(model.OrderHint, 12, ink: ColorToken.Muted, wrap: true)
            )
        );
        if (model.RecordText is { } record)
        {
            // EDI-010: «Grabar con teclado», or Cancelar while the window listens to the keyboard.
            var toggle = Ui.Button(
                Ui.IconLabel(
                    model.Recording is null ? "radio_button_checked" : "close",
                    record,
                    16,
                    13,
                    bold: false
                ),
                record,
                editor.ToggleRecording,
                stroke: ColorToken.Border,
                radius: 8
            );
            var hint = Ui.Text(model.RecordHint, 12, ink: ColorToken.Muted, wrap: true);
            hint.Margin = new Thickness(10, 0, 0, 0);
            hint.VerticalAlignment = VerticalAlignment.Center;
            var line = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(toggle, Dock.Left);
            line.Children.Add(toggle);
            line.Children.Add(hint);
            column.Children.Add(line);
        }

        return column;
    }

    private static CcToggle KeyButton(KeyCell cell, IComboEditor editor)
    {
        var label = Ui.Text(cell.Label, 12, mono: true, wrap: true);
        label.TextAlignment = TextAlignment.Center;
        var button = Ui.Choice(
            label,
            cell.AccessibleName,
            cell.Chosen,
            () => editor.TapKey(cell.Key),
            44,
            8
        );
        button.Padding = new Thickness(4);
        return button;
    }
}
