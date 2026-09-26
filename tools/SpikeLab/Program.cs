using System.Windows;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Tools.SpikeLab;

/// <summary>
/// Entry point of the spike laboratory (docs/testing/spikes/S1.md, S3.md, S4.md). The spikelab package composes the
/// lab surfaces, the orchestrator and the adapters here; until then it only opens the lab window.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // Also set in the runtime configuration; repeated here so the lab fails loudly if that is ever lost.
        PointerSetup.DisableStylusAndTouchSupport();

        var application = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose,
        };
        return application.Run(new LabWindow());
    }
}
