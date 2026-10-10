namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Deleting the data when Clícalo is uninstalled (NFR-010, proposal P6, ADR-0029). The data is kept by default: it is
/// only deleted when the person asked for it in Sistema › Desinstalar, with two taps and after saving a copy where
/// they chose (REG-04, REG-08). That request is a marker file in the local data folder; the uninstaller's hook, which
/// cannot show any UI, deletes the data folders only if it finds it. Uninstalling from Windows Settings never writes
/// the marker, so that way always keeps the data.
/// </summary>
public static class UninstallDataWipe
{
    /// <summary>The name of the marker file, in <c>%LocalAppData%\Clicalo</c>.</summary>
    public const string MarkerName = "delete-data-on-uninstall";

    /// <summary>What the marker file holds: its presence is the request.</summary>
    public static ReadOnlyMemory<byte> Marker { get; } = "clicalo.uninstall.delete-data\n"u8.ToArray();

    /// <summary>Where the marker of <paramref name="locations"/> goes.</summary>
    /// <param name="locations">The data folders.</param>
    public static string MarkerPath(DataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        return Path.Combine(locations.LocalRoot ?? locations.Root, MarkerName);
    }

    /// <summary>Whether deleting the data was asked for.</summary>
    /// <param name="locations">The data folders.</param>
    public static bool IsRequested(DataLocations locations) => File.Exists(MarkerPath(locations));

    /// <summary>
    /// Takes the request back (the uninstaller did not start, or a normal start found a stale marker): from then on
    /// uninstalling keeps the data again. Never throws.
    /// </summary>
    /// <param name="locations">The data folders.</param>
    public static void Withdraw(DataLocations locations)
    {
        try
        {
            File.Delete(MarkerPath(locations));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left in place: the next start tries again.
        }
    }

    /// <summary>
    /// Deletes the data folders when it was asked for, the marker last; without the marker nothing is touched. Never
    /// throws: what cannot be deleted stays.
    /// </summary>
    /// <param name="locations">The data folders.</param>
    /// <returns>Whether the data was asked to be deleted.</returns>
    public static bool RunIfRequested(DataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        if (!IsRequested(locations))
        {
            return false;
        }

        DeleteFolder(locations.Root);
        if (locations.LocalRoot is { } local)
        {
            DeleteFolder(local);
        }

        Withdraw(locations);
        return true;
    }

    /// <summary>
    /// Deletes a data folder with everything in it. A drive root, a folder directly under it or a link to another
    /// folder is never deleted: the data folders of the product are always deeper.
    /// </summary>
    private static void DeleteFolder(string folder)
    {
        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            if (
                Path.GetDirectoryName(full) is not { Length: > 0 } parent
                || string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty),
                    Path.TrimEndingDirectorySeparator(parent),
                    StringComparison.OrdinalIgnoreCase
                )
                || !Directory.Exists(full)
                || File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint)
            )
            {
                return;
            }

            Directory.Delete(full, recursive: true);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            // What another process holds stays; the rest is gone.
        }
    }
}
