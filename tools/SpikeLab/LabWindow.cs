using System.Windows;
using System.Windows.Controls;

namespace Clicalo.Tools.SpikeLab;

/// <summary>
/// The control window of the laboratory: a normal, activatable window from which the maintainer opens the lab
/// surfaces and starts each scripted cycle of S1, S3 and S4. The spikelab package fills it
/// (docs/testing/spikes/M1-ownership.md).
/// </summary>
internal sealed class LabWindow : Window
{
    public LabWindow()
    {
        Title = "Clícalo SpikeLab";
        Width = 480;
        Height = 320;
        Content = new TextBlock
        {
            Text = "Laboratorio de los spikes S1, S3 y S4 (M1). Guiones en docs/testing/spikes/.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24),
            FontSize = 20,
        };
    }
}
