using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.Library;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Shortcuts;

/// <summary>
/// The profiles of «Atajos» (ATJ-002, REP-003): the notice of repeated combinations with its «Revisar» at the top, the
/// overline, a row of 52 per list (icon, name and where it acts, each in one line with «…»; identifiers such as
/// WINWORD.EXE in the type of the text, never in monospace) and «Nuevo perfil». The same list fills the column of the
/// wide window and the layer of the profile selector of the narrow one.
/// </summary>
internal static class ProfilesList
{
    /// <summary>The list of <paramref name="model"/>.</summary>
    /// <param name="model">The profiles column.</param>
    /// <param name="top">The top bar, with the notice of repeated combinations.</param>
    /// <param name="review">Opens the first repeated combination.</param>
    /// <param name="select">Opens a list.</param>
    /// <param name="newProfile">«Nuevo perfil».</param>
    public static StackPanel Build(
        ProfilesModel model,
        TopBarModel top,
        Action review,
        Action<ListRef> select,
        Action newProfile
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(top);
        ArgumentNullException.ThrowIfNull(select);
        var column = new StackPanel();
        if (top.Duplicates is { } duplicates)
        {
            column.Children.Add(Repeated(duplicates, top.ReviewText, top.ReviewName, review));
        }

        var title = new Overline(model.Title)
        {
            Margin = new Thickness(Space.S2, 0, Space.S2, Space.S2),
        };
        column.Children.Add(title);
        foreach (var row in model.Rows)
        {
            var button = new OptionButton
            {
                Kind = OptionKind.ListRow,
                Content = row.Name,
                Symbol = row.Icon,
                Caption = row.Sub,
                IsSelected = row.Selected,
                Margin = new Thickness(0, 0, 0, Space.S1),
            };
            AutomationProperties.SetName(button, row.Name);
            AutomationProperties.SetHelpText(button, row.Sub);
            button.Click += (_, _) => select(row.List);
            column.Children.Add(button);
        }

        var add = new TouchButton
        {
            Content = model.NewProfile,
            Symbol = "add",
            Variant = ButtonVariant.Tertiary,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, Space.S1, 0, 0),
        };
        AutomationProperties.SetName(add, model.NewProfile);
        add.Click += (_, _) => newProfile();
        column.Children.Add(add);
        return column;
    }

    /// <summary>
    /// The notice of repeated combinations (REP-003): attention in warn, at the head of the profiles, with
    /// «Revisar», which opens the first one in the editor.
    /// </summary>
    private static Border Repeated(string message, string action, string name, Action review)
    {
        var icon = Ui.Icon("difference", IconBox.Small, ColorToken.WarnText);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 0, Space.S2, 0);
        var words = new StyledText(message, TextStyleToken.Label) { MaxLines = 0 };
        var button = new TouchButton
        {
            Content = action,
            Variant = ButtonVariant.Tertiary,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(-Space.S3, Space.S1, 0, 0),
        };
        AutomationProperties.SetName(button, name.Length > 0 ? name : action);
        button.Click += (_, _) => review();
        var body = new StackPanel();
        body.Children.Add(words);
        body.Children.Add(button);
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        line.Children.Add(icon);
        line.Children.Add(body);
        var card = Ui.Card(
            line,
            ColorToken.WarnWash,
            ColorToken.Warn,
            Radii.Medium,
            new Thickness(Space.S3, Space.S3, Space.S2, Space.S1)
        );
        card.Margin = new Thickness(0, 0, 0, Space.S3);
        return card;
    }
}
