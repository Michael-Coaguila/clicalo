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
        // The pointer configuration of the product, before the first window (blueprint §8.3): WPF's stylus and touch
        // stacks off (also in the runtime configuration; repeated so the lab never runs without it) and the mouse
        // routed through WM_POINTER, so a click on a surface is a pointer frame like a finger. The board shows both.
        PointerSetup.DisableStylusAndTouchSupport();
        _ = PointerSetup.EnableMouseInPointer();

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
