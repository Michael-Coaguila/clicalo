using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.Editor;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Editor;

/// <summary>
/// The combination box with its key picker (docs/05 §1 B, EDI-007 to EDI-010) in direction C: the field with the keys
/// as keycaps of 44 with their ×, «2 teclas · se guarda solo», ⌫ and «Limpiar» in its foot; the warning of a blocked or
/// special combination; the modifiers as options of 44 with ✓; the groups as chips; the keys of the group; and «Grabar
/// con teclado». The editor of a shortcut and a row of the preview of Plantillas (PLA-016) draw the same box, each with
/// its own <see cref="IComboEditor"/>. JetBrains Mono only for the keys.
/// </summary>
internal static class ComboView
{
    /// <summary>Draws <paramref name="model"/>; every tap goes to <paramref name="editor"/>.</summary>
    /// <param name="model">The combination box.</param>
    /// <param name="editor">Who owns the combination.</param>
    public static StackPanel Build(ComboModel model, IComboEditor editor)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(editor);
        var column = new StackPanel();
        column.Children.Add(new Overline(model.Title));
        var field = Field(model, editor);
        field.Margin = new Thickness(0, Space.S2, 0, 0);
        column.Children.Add(field);
        if (model.WarningText is { } warning)
        {
            var danger = model.Warning == WarningTone.Danger;
            var card = Ui.Card(
                Parts.IconLine(
                    danger ? "block" : "info",
                    warning,
                    danger ? ColorToken.DangerText : ColorToken.WarnText,
                    ColorToken.Text
                ),
                danger ? ColorToken.DangerWash : ColorToken.WarnWash,
                danger ? ColorToken.Danger : ColorToken.Warn,
                Radii.Medium,
                new Thickness(Space.S3)
            );
            card.Margin = new Thickness(0, Space.S2, 0, 0);
            AutomationProperties.SetLiveSetting(
                card,
                danger ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite
            );
            column.Children.Add(card);
        }

        if (model.StepEditing is { } step)
        {
            var done = Parts.Button(
                model.DoneText,
                null,
                ButtonVariant.Primary,
                editor.StopEditingStep
            );
            var strip = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(done, Dock.Right);
            strip.Children.Add(done);
            strip.Children.Add(
                Parts.IconLine(
                    "edit",
                    step,
                    ColorToken.AccentInk,
                    ColorToken.Text,
                    TextStyleToken.Label
                )
            );
            ((FrameworkElement)strip.Children[1]).VerticalAlignment = VerticalAlignment.Center;
            var card = Ui.Card(
                strip,
                ColorToken.AccentWash,
                null,
                Radii.Medium,
                new Thickness(Space.S3, Space.S1, Space.S1, Space.S1)
            );
            card.Margin = new Thickness(0, Space.S2, 0, 0);
            column.Children.Add(card);
        }

        // Four across, as in the design; in a narrow preview of a template they would be under 44, so the grid
        // wraps them (REG-02).
        var modifiers = new AutoFillGrid { Columns = 4, Gap = Space.S2 };
        foreach (var cell in model.Modifiers)
        {
            modifiers.Children.Add(Key(cell, editor));
        }

        modifiers.Margin = new Thickness(0, Space.S3, 0, 0);
        column.Children.Add(modifiers);
        var groups = new WrapPanel { Margin = new Thickness(0, Space.S3, -Space.S2, -Space.S2) };
        foreach (var group in model.Groups)
        {
            var chip = new OptionButton
            {
                Kind = OptionKind.Chip,
                Content = group.Label,
                IsSelected = group.Selected,
                Margin = new Thickness(0, 0, Space.S2, Space.S2),
            };
            AutomationProperties.SetName(chip, group.Label);
            chip.Click += (_, _) => editor.ChooseGroup(group.Group);
            groups.Children.Add(chip);
        }

        column.Children.Add(groups);
        var cells = new AutoFillGrid
        {
            Columns = model.Columns,
            MinItemWidth = ControlHeight.Small,
            Gap = Space.S1,
            Margin = new Thickness(0, Space.S3, 0, 0),
        };
        foreach (var cell in model.Cells)
        {
            cells.Children.Add(Key(cell, editor));
        }

        column.Children.Add(cells);
        var order = Parts.IconLine(
            "format_list_numbered",
            model.OrderHint,
            style: TextStyleToken.Caption
        );
        order.Margin = new Thickness(0, Space.S3, 0, 0);
        column.Children.Add(order);
        if (model.RecordText is { } record)
        {
            // EDI-010: «Grabar con teclado», or Cancelar while the window listens to the keyboard.
            var toggle = Parts.Button(
                record,
                model.Recording is null ? "radio_button_checked" : "close",
                ButtonVariant.Secondary,
                editor.ToggleRecording
            );
            toggle.VerticalAlignment = VerticalAlignment.Top;
            var hint = Parts.Note(model.RecordHint, style: TextStyleToken.Caption);
            hint.Margin = new Thickness(Space.S3, 0, 0, 0);
            hint.VerticalAlignment = VerticalAlignment.Center;
            var line = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(0, Space.S3, 0, 0),
            };
            DockPanel.SetDock(toggle, Dock.Left);
            line.Children.Add(toggle);
            line.Children.Add(hint);
            column.Children.Add(line);
        }

        return column;
    }

    /// <summary>
    /// The field: a strip when a combination replaces another, the keys in press order (or what it waits for), and its
    /// foot with the count, ⌫ and «Limpiar». While it records, it has the single look of «elegido».
    /// </summary>
    private static Border Field(ComboModel model, IComboEditor editor)
    {
        var box = new StackPanel();
        if (model.Replacing is { } replacing)
        {
            var keep = Parts.Button(
                model.KeepOldText,
                null,
                ButtonVariant.Tertiary,
                editor.KeepOldCombination
            );
            var strip = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(keep, Dock.Right);
            strip.Children.Add(keep);
            var words = Parts.IconLine(
                "swap_horiz",
                replacing,
                ColorToken.WarnText,
                ColorToken.Text
            );
            words.VerticalAlignment = VerticalAlignment.Center;
            strip.Children.Add(words);
            var band = new Border { Child = strip, Padding = new Thickness(Space.S3, 0, 0, 0) };
            Ui.Ink(band, Border.BackgroundProperty, ColorToken.WarnWash);
            box.Children.Add(band);
        }

        var keys = new WrapPanel
        {
            Margin = new Thickness(Space.S3, Space.S3, Space.S1, Space.S1),
            MinHeight = ControlHeight.Large - Space.S2,
        };
        if (model.Recording is { } recording)
        {
            var listening = Parts.Note(recording, ColorToken.AccentInk, TextStyleToken.Body);
            listening.Margin = new Thickness(0, 0, Space.S2, Space.S2);
            AutomationProperties.SetLiveSetting(listening, AutomationLiveSetting.Polite);
            keys.Children.Add(listening);
        }

        if (model.Empty is { } empty)
        {
            var none = Parts.Note(empty, style: TextStyleToken.Body);
            none.Margin = new Thickness(0, Space.S2, Space.S2, Space.S2);
            keys.Children.Add(none);
        }

        foreach (var chip in model.Chips)
        {
            if (chip.Index > 0)
            {
                var plus = new DecorativeText
                {
                    Text = "+",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, Space.S2, Space.S2),
                };
                StyledText.Apply(plus, TextStyleToken.Label);
                Ui.Ink(plus, TextBlock.ForegroundProperty, ColorToken.Muted);
                keys.Children.Add(plus);
            }

            keys.Children.Add(
                Keycap(chip.Label, chip.RemoveName, () => editor.RemoveKey(chip.Index))
            );
        }

        box.Children.Add(keys);
        var back = Parts.Button(
            string.Empty,
            "backspace",
            ButtonVariant.Tertiary,
            editor.RemoveLastKey,
            model.BackName
        );
        back.IsEnabled = model.HasKeys;
        var clear = Parts.Button(
            model.ClearText,
            "restart_alt",
            ButtonVariant.Tertiary,
            editor.ClearKeys
        );
        clear.IsEnabled = model.HasKeys;
        var count = new StyledText(model.Count, TextStyleToken.Caption, ColorToken.Muted)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        var foot = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(clear, Dock.Right);
        DockPanel.SetDock(back, Dock.Right);
        foot.Children.Add(clear);
        foot.Children.Add(back);
        foot.Children.Add(count);
        var footer = new Border
        {
            Child = foot,
            MinHeight = ControlHeight.Medium,
            Padding = new Thickness(Space.S3, 0, Space.S1, 0),
            BorderThickness = new Thickness(0, Theming.Generated.Stroke.Line, 0, 0),
        };
        Ui.Ink(footer, Border.BorderBrushProperty, ColorToken.Border);
        box.Children.Add(footer);
        var listens = model.Recording is not null;
        return Ui.Card(
            box,
            listens ? ColorToken.AccentWash : ColorToken.Field,
            listens ? ColorToken.Accent : ColorToken.Line,
            Radii.Medium,
            new Thickness(0),
            listens ? Theming.Generated.Stroke.Selected : Theming.Generated.Stroke.Line
        );
    }

    /// <summary>A key of the combination: its name in JetBrains Mono and ×, which takes it out.</summary>
    private static TouchButton Keycap(string label, string removeName, Action remove)
    {
        var name = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = AppFonts.MonoWeight,
        };
        name.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
        name.SetResourceReference(
            TextBlock.FontSizeProperty,
            ThemeKeys.FontSize(TextStyleToken.Label)
        );
        var x = Ui.Icon("close", IconBox.Small, ColorToken.Muted);
        x.Margin = new Thickness(Space.S2, 0, 0, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(name);
        row.Children.Add(x);
        var key = new TouchButton
        {
            Content = row,
            Variant = ButtonVariant.Secondary,
            Padding = new Thickness(Space.S3, 0, Space.S3, 0),
            Margin = new Thickness(0, 0, Space.S2, Space.S2),
        };
        AutomationProperties.SetName(key, removeName);
        key.Click += (_, _) => remove();
        return key;
    }

    private static OptionButton Key(KeyCell cell, IComboEditor editor)
    {
        var button = new OptionButton
        {
            Kind = OptionKind.Tile,
            Content = cell.Label,
            IsSelected = cell.Chosen,
            IsMonospace = true,
            Role = OptionRole.Toggle,
            Padding = new Thickness(Space.S1, 0, Space.S1, 0),
        };
        AutomationProperties.SetName(button, cell.AccessibleName);
        button.Click += (_, _) => editor.TapKey(cell.Key);
        return button;
    }
}
