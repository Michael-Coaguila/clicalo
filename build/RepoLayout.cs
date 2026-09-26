namespace Clicalo.Build;

/// <summary>Well-known paths of the repository, resolved from the folder that contains <c>Clicalo.slnx</c>.</summary>
internal sealed class RepoLayout
{
    /// <summary>File name that marks the repository root.</summary>
    public const string SolutionFileName = "Clicalo.slnx";

    private RepoLayout(string root) =>
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    /// <summary>Absolute path of the repository root.</summary>
    public string Root { get; }

    /// <summary>The full solution.</summary>
    public string Solution => Path.Combine(Root, SolutionFileName);

    /// <summary>The fast inner-loop filter: pure layers and their tests.</summary>
    public string CoreFilter => Path.Combine(Root, "Core.slnf");

    /// <summary>Every build output (UseArtifactsOutput).</summary>
    public string Artifacts => Path.Combine(Root, "artifacts");

    /// <summary>Everything <c>cl</c> writes for people to read: reports, logs and test results.</summary>
    public string ClDirectory => Path.Combine(Artifacts, "cl");

    /// <summary>The Markdown report of the last failure.</summary>
    public string LastErrorFile => Path.Combine(ClDirectory, "last-error.md");

    /// <summary>Pending manual steps found by <c>cl setup</c>.</summary>
    public string SetupNotesFile => Path.Combine(ClDirectory, "setup.md");

    /// <summary>Machine logs (MSBuild errors) behind the reports.</summary>
    public string LogsDirectory => Path.Combine(ClDirectory, "logs");

    /// <summary>TRX files of the last test run.</summary>
    public string TestResultsDirectory => Path.Combine(ClDirectory, "test-results");

    /// <summary>Finds the repository root walking up from <paramref name="startDirectory"/>.</summary>
    /// <exception cref="InvalidOperationException">No ancestor contains <see cref="SolutionFileName"/>.</exception>
    public static RepoLayout Locate(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return new RepoLayout(directory.FullName);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root (" + SolutionFileName + ") from " + startDirectory
        );
    }

    /// <summary>Uses <paramref name="root"/> as the repository root without probing the file system.</summary>
    public static RepoLayout FromRoot(string root) => new(root);

    /// <summary>
    /// Path of <paramref name="path"/> relative to the root, with the platform separator, as it is read aloud
    /// in final lines (for example <c>artifacts\cl\last-error.md</c> on Windows).
    /// </summary>
    public string Relative(string path) => Path.GetRelativePath(Root, path);

    /// <summary>Path of <paramref name="path"/> relative to the root with forward slashes, for Markdown.</summary>
    public string RelativeForward(string path) => Relative(path).Replace('\\', '/');
}
