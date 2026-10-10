using System.IO;
using Clicalo.App.Composition;
using Clicalo.App.Lifecycle;
using Clicalo.App.SingleInstance;
using Clicalo.Infrastructure.Logging;
using Clicalo.UI.Wpf.Pointer;
using Microsoft.Extensions.Logging;
using Serilog;
using Velopack;

namespace Clicalo.App;

/// <summary>
/// The entry point of <c>Clicalo.exe</c> (blueprint §3.1, §3.4). Before anything else the pointer configuration of the
/// product (§8.3): WPF's stylus and touch stacks off and the mouse routed through <c>WM_POINTER</c>, so a click is a
/// pointer frame like a finger. Then the single instance (SIS-003, NFR-018): the first process of the session runs;
/// a second start asks it to show the panel through the pipe and ends, without opening the log. Before all of it, the
/// installer's hooks (Velopack, ADR-0027): installing, updating and uninstalling end the process there; uninstalling
/// removes the «Iniciar con Windows» entry and keeps the data in <c>%AppData%\Clicalo</c>, unless the person asked in
/// Sistema › Desinstalar to delete it after saving a copy (ADR-0029). The first start after installing is noted, so
/// the welcome can ask what to do with data from before (P6). An elevated instance
/// of «Reabrir como administrador» waits for the one it replaces (<c>--handover</c>, user decision D7).
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Velopack's hooks first, here in Main where vpk pack looks for them (ADR-0027, ADR-0029).
        var firstRunAfterInstall = false;
        VelopackApp
            .Build()
            .SetArgs(args)
            .SetAutoApplyOnStartup(false)
            .OnFirstRun(_ => firstRunAfterInstall = true)
            .OnBeforeUninstallFastCallback(_ => SystemUninstall.OnUninstalling())
            .Run();
        PointerSetup.DisableStylusAndTouchSupport();
        _ = PointerSetup.EnableMouseInPointer();

        var options = AppOptions.Parse(args, DefaultDataDirectory());
        var identity = InstanceIdentity.Current();
        ElevationHandover.WaitForPrevious(args);
        var mutex = InstanceMutex.Open(identity);
        if (!mutex.IsFirst)
        {
            mutex.Dispose();
            return (int)Shutdown.SecondStart.ShowRunningInstance(identity);
        }

        // The product log (LOG-001..LOG-006): codes and counts only, the Windows user removed, a fixed-name rolling
        // file under logs\ of the data folder.
        var locations = AppDataLocations.For(options);
        var product = ClicaloLog.Create(locations, ClicaloLog.DefaultLevel());
        using var logs = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Information).AddSerilog(product, dispose: true)
        );
        using var host = new AppHost(options, identity, mutex, logs, firstRunAfterInstall);
        return host.Run();
    }

    /// <summary><c>%AppData%\Clicalo</c> (blueprint §6.5): roaming, kept when Clícalo is uninstalled.</summary>
    private static string DefaultDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clicalo"
        );
}
