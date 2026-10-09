using Velopack.Locators;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// The installation of <c>Clicalo.exe</c> (ADR-0012, ADR-0027), once Velopack's hooks ran in <c>Program.Main</c>: the
/// installed executable, which «Iniciar con Windows» and «Reabrir como administrador» use as the only path they ever
/// start. Uninstalling only removes <c>%LocalAppData%\Clicalo.App</c>: the data in <c>%AppData%\Clicalo</c> is kept.
/// </summary>
public static class Installation
{
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
