using Velopack;
using Velopack.Locators;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// The installer's side of the start of <c>Clicalo.exe</c> (ADR-0012, ADR-0027): Velopack's hooks run first (install,
/// update and uninstall end the process right there), and the installed executable is located, which «Iniciar con
/// Windows» and «Reabrir como administrador» use as the only path they ever start.
/// </summary>
public static class Installation
{
    /// <summary>
    /// Runs Velopack's start: a hook of the installer runs <paramref name="beforeUninstall"/> (or nothing) and ends the
    /// process; a normal start returns at once. The data in <c>%AppData%\Clicalo</c> is never touched: uninstalling
    /// only removes <c>%LocalAppData%\Clicalo.App</c>, so the person's data is kept by default.
    /// </summary>
    /// <param name="arguments">The command line.</param>
    /// <param name="beforeUninstall">Removes what lives outside the install folder (the <c>Run</c> entry).</param>
    public static void RunHooks(string[] arguments, Action beforeUninstall)
    {
        ArgumentNullException.ThrowIfNull(beforeUninstall);
        VelopackApp
            .Build()
            .SetArgs(arguments)
            .SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(_ => beforeUninstall())
            .Run();
    }

    /// <summary>
    /// The installed <c>Clicalo.exe</c> (<c>%LocalAppData%\Clicalo.App\current\Clicalo.exe</c>), or null when this copy
    /// was not installed with the installer (a development build, a test).
    /// </summary>
    public static string? InstalledExecutable()
    {
        try
        {
            if (!VelopackLocator.IsCurrentSet)
            {
                return null;
            }

            var locator = VelopackLocator.Current;
            return locator.CurrentlyInstalledVersion is null || locator.AppContentDir is null
                ? null
                : Path.Combine(locator.AppContentDir, UpdateChannels.MainExe);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
