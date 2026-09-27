using System.IO;
using Clicalo.Infrastructure.Persistence;

namespace Clicalo.App.Composition;

/// <summary>Where the data of this start lives (blueprint §6.5).</summary>
internal static class AppDataLocations
{
    /// <summary>
    /// The production folders (<c>%AppData%\Clicalo</c>, with <c>pending\</c> and the crash journal under
    /// <c>%LocalAppData%\Clicalo</c>), or everything inside the folder of <c>--data</c>, so a development or measurement
    /// start never touches the user's data.
    /// </summary>
    /// <param name="options">The command line.</param>
    public static DataLocations For(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.IsolatedData
            ? new DataLocations(options.DataDirectory)
            : DataLocations.ForCurrentUser();
    }

    /// <summary>
    /// The mark of a v1 migration that failed (<c>migration-v1.pending</c>, blueprint §6.6, D-21): while it exists, a
    /// start with <c>--migrate-v1</c> tries again. In the local data folder, like the v1 file it names.
    /// </summary>
    /// <param name="locations">The data folders.</param>
    public static string PendingMigration(DataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        return Path.Combine(locations.LocalRoot ?? locations.Root, "migration-v1.pending");
    }

    /// <summary>
    /// The crash journal Sentinel reads to detect a crash loop (ADR-0018, <c>CrashJournal</c>): in the local data
    /// folder, which Sentinel reads from <c>%LocalAppData%\Clicalo</c>.
    /// </summary>
    /// <param name="locations">The data folders.</param>
    public static string CrashJournal(DataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        return Path.Combine(
            locations.LocalRoot ?? locations.Root,
            Platform.Core.Guardian.CrashJournal.FileName
        );
    }
}
