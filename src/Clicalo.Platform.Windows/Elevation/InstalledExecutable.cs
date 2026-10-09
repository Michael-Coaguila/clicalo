namespace Clicalo.Platform.Windows.Elevation;

/// <summary>
/// The check before anything is started elevated (blueprint §3.3 rule 2 as amended by ADR-0027): without a code
/// signature in 2.0 (user decision D6), the process must be the installed copy itself, <c>Clicalo.exe</c> under
/// <c>%LocalAppData%\Clicalo.App\current</c>, the file must exist, and neither it nor any folder above it may be a link
/// (a junction or symbolic link could send the UAC prompt to another file with the name of Clícalo).
/// </summary>
internal static class InstalledExecutable
{
    /// <summary>Whether <paramref name="running"/> is exactly <paramref name="installed"/> and may be started elevated.</summary>
    /// <param name="installed">The installed executable, or null for a copy that is not installed.</param>
    /// <param name="running">The executable of this process.</param>
    /// <param name="attributes">The attributes of a path, or null when it does not exist (tests fake the disk).</param>
    public static bool IsVerified(
        string? installed,
        string? running,
        Func<string, FileAttributes?> attributes
    )
    {
        ArgumentNullException.ThrowIfNull(attributes);
        if (string.IsNullOrWhiteSpace(installed) || string.IsNullOrWhiteSpace(running))
        {
            return false;
        }

        string expected;
        string actual;
        try
        {
            expected = Path.GetFullPath(installed);
            actual = Path.GetFullPath(running);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (attributes(expected) is not { } file || file.HasFlag(FileAttributes.Directory))
        {
            return false;
        }

        for (var path = expected; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
        {
            if (attributes(path) is not { } current || current.HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The attributes of <paramref name="path"/> on the disk, or null when it does not exist.</summary>
    /// <param name="path">A file or folder.</param>
    public static FileAttributes? OnDisk(string path)
    {
        try
        {
            return File.Exists(path) || Directory.Exists(path) ? File.GetAttributes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
