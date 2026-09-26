namespace Clicalo.TestKit;

/// <summary>Locates repository folders from a running test, independent of the output directory.</summary>
public static class RepoPaths
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    /// <summary>Absolute path of the repository root (the folder that contains <c>Clicalo.slnx</c>).</summary>
    public static string Root => RootPath.Value;

    /// <summary>Absolute path of <c>data/</c>, the versioned non-code source of truth.</summary>
    public static string Data => Path.Combine(Root, "data");

    /// <summary>Absolute path of the original design handoff (read-only reference).</summary>
    public static string Handoff => Path.Combine(Root, "docs", "design", "handoff");

    /// <summary>Combines <paramref name="parts"/> under the repository root.</summary>
    public static string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Clicalo.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root (Clicalo.slnx) from " + AppContext.BaseDirectory
        );
    }
}
