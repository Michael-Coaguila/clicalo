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
            return locator.CurrentlyInstalledVersion is null
                ? null
                : InFolder(
                    locator.AppContentDir,
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                );
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    /// <c>Clicalo.exe</c> in <paramref name="contentFolder"/> only when that folder is exactly the one the installer uses,
    /// <c>%LocalAppData%\Clicalo.App\current</c>: a portable copy or one unpacked elsewhere is never treated as installed
    /// (it never registers in <c>Run</c> nor reopens as administrator, ADR-0027).
    /// </summary>
    /// <param name="contentFolder">The folder Velopack reports for the running copy.</param>
    /// <param name="localAppData">The user's <c>%LocalAppData%</c>.</param>
    internal static string? InFolder(string? contentFolder, string? localAppData)
    {
        if (string.IsNullOrWhiteSpace(contentFolder) || string.IsNullOrWhiteSpace(localAppData))
        {
            return null;
        }

        var expected = Path.GetFullPath(
            Path.Combine(localAppData, UpdateChannels.PackId, "current")
        );
        var actual = Path.GetFullPath(contentFolder);
        return string.Equals(
            Path.TrimEndingDirectorySeparator(expected),
            Path.TrimEndingDirectorySeparator(actual),
            StringComparison.OrdinalIgnoreCase
        )
            ? Path.Combine(expected, UpdateChannels.MainExe)
            : null;
    }
}
