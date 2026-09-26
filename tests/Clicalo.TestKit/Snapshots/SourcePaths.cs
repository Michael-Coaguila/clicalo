namespace Clicalo.TestKit.Snapshots;

/// <summary>Turns a <c>[CallerFilePath]</c> value into a path on this machine.</summary>
internal static class SourcePaths
{
    // ContinuousIntegrationBuild maps the repository root to "/_/" in compiler-emitted paths.
    private const string MappedRoot = "/_/";

    public static string Resolve(string callerFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerFilePath);
        if (File.Exists(callerFilePath))
        {
            return callerFilePath;
        }

        if (callerFilePath.StartsWith(MappedRoot, StringComparison.Ordinal))
        {
            var relative = callerFilePath[MappedRoot.Length..]
                .Replace('/', Path.DirectorySeparatorChar);
            var local = Path.Combine(RepoPaths.Root, relative);
            if (File.Exists(local))
            {
                return local;
            }
        }

        throw new InvalidOperationException(
            "The test source file '"
                + callerFilePath
                + "' is not on this machine, so its snapshots cannot be located. "
                + "Snapshot tests run from a repository checkout."
        );
    }
}
