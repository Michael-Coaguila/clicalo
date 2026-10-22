using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Shortcuts;

/// <summary>
/// «Tu panel muestra: Siempre visible + Word + Frecuentes» (ATJ-001): one line at every width, a sentence and not a row
/// of ovals (D20): what it shows only tells, so it is text with its icons, not controls. The lead and the «+» keep their
/// width; where the line is short, the parts give up width and end in «…», their muted part first.
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

    /// <summary>A part that only tells (not a control): icon, name and its muted part, without a box.</summary>
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
        var chip = new Border
        {
            Child = line,
            Padding = new Thickness(0, Space.S2, 0, Space.S2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(chip, layer.Label + " " + layer.Meta);
        return chip;
    }
}
