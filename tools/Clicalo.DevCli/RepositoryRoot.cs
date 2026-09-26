namespace Clicalo.DevCli;

/// <summary>Finds the repository root: the closest folder, upwards, that contains <c>Clicalo.slnx</c>.</summary>
internal static class RepositoryRoot
{
    private const string Marker = "Clicalo.slnx";

    public static string? Find(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, Marker)))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    /// <summary>Path relative to the root with forward slashes, as printed in reports.</summary>
    public static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
