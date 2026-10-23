using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Templates;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Editor;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The keyboard the shortcuts are made for (PLA-009, PLA-013, BIE-006): one row that unfolds the layouts and the
/// languages of the programs, side by side, the detected ones marked. Plantillas and the apps step of the welcome draw
/// the same row, each with its own actions.
/// </summary>
internal static class KeyboardChooser
{
    /// <summary>The groups of choices side by side: the layouts and the languages of the programs.</summary>
    private const int Groups = 2;

    /// <summary>The row of <paramref name="model"/>.</summary>
    /// <param name="model">The keyboard line.</param>
    /// <param name="toggle">[change] / [done].</param>
    /// <param name="setLayout">A layout.</param>
    /// <param name="setApps">A language of the programs.</param>
    public static StackPanel Build(
        KeyboardModel model,
        Action toggle,
        Action<string> setLayout,
        Action<string> setApps
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(toggle);
        var icon = Ui.Icon("keyboard", IconBox.Medium, ColorToken.AccentInk);
        icon.Margin = new Thickness(0, 0, Space.S3, 0);
        var action = new StyledText(model.ActionText, TextStyleToken.Label, ColorToken.AccentInk)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S2, 0, 0, 0),
        };
        var caret = Ui.Icon(
            model.Open ? "expand_less" : "expand_more",
            IconBox.Medium,
            ColorToken.Muted
        );
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new StyledText(model.Line, TextStyleToken.Label));
        lines.Children.Add(
            new StyledText(Parts.Unbroken(model.Why), TextStyleToken.Caption, ColorToken.Muted)
            {
                MaxLines = 2,
            }
        );
        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(caret, Dock.Right);
        DockPanel.SetDock(action, Dock.Right);
        content.Children.Add(icon);
        content.Children.Add(caret);
        content.Children.Add(action);
        content.Children.Add(lines);
        var row = new TouchButton
        {
            Content = content,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Large,
            IsExpanded = model.Open,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S3, Space.S2, Space.S3, Space.S2),
        };
        row.Height = double.NaN;
        row.MinHeight = ControlHeight.Large;
        AutomationProperties.SetName(row, model.Line);
        AutomationProperties.SetHelpText(row, model.Why);
        row.Click += (_, _) => toggle();
        var column = new StackPanel();
        column.Children.Add(row);
        if (model.Open)
        {
            var options = Ui.Columns(
                Groups,
                Space.S4,
                [
                    Options(model.LayoutTitle, model.Layouts, model.DetectedText, setLayout),
                    Options(model.AppsTitle, model.AppsLanguages, model.DetectedText, setApps),
                ]
            );
            options.Margin = new Thickness(0, Space.S4, 0, 0);
            column.Children.Add(options);
        }

        return column;
    }

    private static StackPanel Options(
        string title,
        ValueList<KbOption> options,
        string detected,
        Action<string> choose
    )
    {
        var column = Parts.Section(title);
        foreach (var option in options)
        {
            var button = new OptionButton
            {
                Kind = OptionKind.ListRow,
                Content = option.Label,
                Caption = option.Detected ? detected : null,
                IsSelected = option.Selected,
                Margin = new Thickness(0, Space.S1, 0, 0),
            };
            AutomationProperties.SetName(
                button,
                option.Detected ? option.Label + ", " + detected : option.Label
            );
            button.Click += (_, _) => choose(option.Id);
            column.Children.Add(button);
        }

        return column;
    }
}
