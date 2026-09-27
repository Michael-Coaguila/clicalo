using System.IO;
using Clicalo.App.Lifecycle;
using Clicalo.App.SingleInstance;
using Clicalo.UI.Wpf.Pointer;
using Microsoft.Extensions.Logging;

namespace Clicalo.App;

/// <summary>
/// The entry point of <c>Clicalo.exe</c> (blueprint §3.1, §3.4). Before anything else the pointer configuration of the
/// product (§8.3): WPF's stylus and touch stacks off and the mouse routed through <c>WM_POINTER</c>, so a click is a
/// pointer frame like a finger. Then the single instance (SIS-003, NFR-018): the first process of the session runs;
/// a second start asks it to show the panel through the pipe and ends.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        PointerSetup.DisableStylusAndTouchSupport();
        _ = PointerSetup.EnableMouseInPointer();

        var options = AppOptions.Parse(args, DefaultDataDirectory());
        var identity = InstanceIdentity.Current();
        using var logs = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Information)
        );
        var mutex = InstanceMutex.Open(identity);
        if (!mutex.IsFirst)
        {
            mutex.Dispose();
            return (int)Shutdown.SecondStart.ShowRunningInstance(identity);
        }

        using var host = new AppHost(options, identity, mutex, logs);
        return host.Run();
    }

    /// <summary><c>%AppData%\Clicalo</c> (blueprint §6.5): roaming, kept when Clícalo is uninstalled.</summary>
    private static string DefaultDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clicalo"
        );
}
