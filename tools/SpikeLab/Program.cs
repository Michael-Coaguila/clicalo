using System.Windows;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Views;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Tools.SpikeLab;

/// <summary>
/// Entry point of the spike laboratory (docs/testing/spikes/README.md):
/// <c>dotnet run --project tools\SpikeLab -- --spike S1</c> (or S3, S4; without <c>--spike</c> the control window asks),
/// <c>--reports &lt;carpeta&gt;</c> to change where the reports go, and <c>--check</c> to verify which real pieces are
/// integrated without showing anything.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Also set in the runtime configuration; repeated here so the lab fails loudly if that is ever lost.
        PointerSetup.DisableStylusAndTouchSupport();

        var options = LabOptions.Parse(args, out var error);
        var application = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        if (options.CheckOnly)
        {
            application.Startup += async (_, _) =>
                application.Shutdown(await LabApp.CheckAsync(options, application.Dispatcher));
            return application.Run();
        }

        var app = new LabApp(options, application.Dispatcher);
        var window = new LabWindow(app, error);
        application.DispatcherUnhandledException += (_, unhandled) =>
        {
            // A laboratory keeps running and records the failure: the report is what the maintainer reads later.
            app.Host.Log.Add(
                "error",
                unhandled.Exception.GetType().Name + ": " + unhandled.Exception.Message
            );
            app.Host.Measurements.Notice(
                "Error inesperado de SpikeLab: " + unhandled.Exception.Message
            );
            unhandled.Handled = true;
        };
        application.Startup += async (_, _) =>
        {
            window.Show();
            await app.StartAsync();
        };
        return application.Run();
    }
}
