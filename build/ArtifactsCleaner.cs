namespace Clicalo.Build;

/// <summary>
/// Empties <c>artifacts/</c> except the folders of the running orchestrator, which Windows keeps locked
/// while <c>cl</c> runs (they are rebuilt on demand anyway).
/// </summary>
internal static class ArtifactsCleaner
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(150),
        TimeSpan.FromMilliseconds(600),
    ];

    /// <summary>Deletes everything below <paramref name="artifacts"/> except <paramref name="keep"/>.</summary>
    /// <returns>Paths that could not be deleted, relative to <paramref name="artifacts"/>.</returns>
    public static async Task<IReadOnlyList<string>> CleanAsync(
        string artifacts,
        IReadOnlyCollection<string> keep
    )
    {
        var failures = new List<string>();
        if (!Directory.Exists(artifacts))
        {
            return failures;
        }

        var kept = keep.Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .ToList();
        await CleanDirectoryAsync(Path.GetFullPath(artifacts), kept, failures, artifacts);
        return failures;
    }

    private static async Task CleanDirectoryAsync(
        string directory,
        IReadOnlyList<string> kept,
        List<string> failures,
        string artifacts
    )
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory).ToList())
        {
            var full = Path.TrimEndingDirectorySeparator(entry);
            if (kept.Any(path => string.Equals(path, full, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (kept.Any(path => IsAncestor(full, path)))
            {
                await CleanDirectoryAsync(full, kept, failures, artifacts);
                continue;
            }

            await DeleteTreeAsync(full, failures, artifacts);
        }
    }

    /// <summary>
    /// Deletes <paramref name="path"/>; when a folder cannot go at once, deletes what it can inside and
    /// reports only the entries that are really in use, so the report names the locked file.
    /// </summary>
    private static async Task DeleteTreeAsync(string path, List<string> failures, string artifacts)
    {
        if (await TryDeleteAsync(path))
        {
            return;
        }

        if (!Directory.Exists(path))
        {
            failures.Add(Path.GetRelativePath(artifacts, path));
            return;
        }

        var before = failures.Count;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path).ToList())
        {
            await DeleteTreeAsync(entry, failures, artifacts);
        }

        if (!await TryDeleteAsync(path) && failures.Count == before)
        {
            failures.Add(Path.GetRelativePath(artifacts, path));
        }
    }

    private static async Task<bool> TryDeleteAsync(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt >= RetryDelays.Length)
                {
                    return false;
                }

                await Task.Delay(RetryDelays[attempt]);
            }
        }
    }

    private static bool IsAncestor(string candidate, string path) =>
        path.StartsWith(
            candidate + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );
}
