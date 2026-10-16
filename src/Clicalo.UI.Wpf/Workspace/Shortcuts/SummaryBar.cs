using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Shortcuts;

/// <summary>
/// «Tu panel muestra: Siempre visible + Word + Frecuentes» (ATJ-001): one line at every width. The lead and the «+»
/// keep their width; where the line is short, the chips give up width and end in «…», their muted part first.
/// </summary>
internal static class SummaryBar
{
    /// <summary>The line of <paramref name="model"/>.</summary>
    /// <param name="model">The top bar of «Atajos».</param>
    public static ShareRow Build(TopBarModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var row = new ShareRow { Gap = Space.S2, VerticalAlignment = VerticalAlignment.Center };
        var lead = new StyledText(model.Title, TextStyleToken.Label, ColorToken.Muted)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        ShareRow.SetCanShrink(lead, false);
        row.Children.Add(lead);
        for (var i = 0; i < model.Layers.Count; i++)
        {
            if (i > 0)
            {
                var plus = new DecorativeText
                {
                    Text = "+",
                    VerticalAlignment = VerticalAlignment.Center,
                };
                StyledText.Apply(plus, TextStyleToken.Label);
                Ui.Ink(plus, TextBlock.ForegroundProperty, ColorToken.Muted);
                ShareRow.SetCanShrink(plus, false);
                row.Children.Add(plus);
            }

            row.Children.Add(Chip(model.Layers[i]));
        }

        return row;
    }

    /// <summary>A chip that only tells (not a control): icon, name and its muted part, on a pill of the card.</summary>
    private static Border Chip(LayerChip layer)
    {
        var icon = Ui.Icon(layer.Icon, IconBox.Small, ColorToken.Muted);
        icon.Margin = new Thickness(0, 0, Space.S2, 0);
        var name = new StyledText(layer.Label, TextStyleToken.Label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        var meta = new StyledText(layer.Meta, TextStyleToken.BodySmall, ColorToken.Muted)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S1, 0, 0, 0),
        };
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(name, Dock.Left);
        line.Children.Add(icon);
        line.Children.Add(name);
        line.Children.Add(meta);
        var chip = Ui.Card(
            line,
            ColorToken.Card,
            ColorToken.Border,
            Radii.Pill,
            new Thickness(Space.S3, Space.S2, Space.S3, Space.S2)
        );
        chip.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(chip, layer.Label + " " + layer.Meta);
        return chip;
    }
}
