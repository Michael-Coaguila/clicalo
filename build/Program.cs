using System.Text;

namespace Clicalo.Build;

/// <summary>Entry point of <c>cl</c> (run through <c>cl.cmd</c> or <c>cl.ps1</c> at the repository root).</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var originalEncoding = Console.OutputEncoding;
        // Child tools and CI logs agree on UTF-8, so Spanish text and paths survive redirection.
        TrySetOutputEncoding(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            var layout = RepoLayout.Locate(AppContext.BaseDirectory);
            Environment.CurrentDirectory = layout.Root;

            // No MSBuild node outlives a verb: no file locks for `cl clean`, no state shared across worktrees.
            Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

            var application = new ClApplication(
                layout,
                Console.Out,
                TimeProvider.System,
                OwnOutputFolders(layout)
            );
            return await application.RunAsync(args);
        }
        finally
        {
            TrySetOutputEncoding(originalEncoding);
        }
    }

    /// <summary>
    /// The orchestrator's own bin and obj folders (<c>artifacts/bin/Build</c> and <c>artifacts/obj/Build</c>),
    /// which Windows keeps locked while it runs.
    /// </summary>
    private static IReadOnlyCollection<string> OwnOutputFolders(RepoLayout layout)
    {
        // AppContext.BaseDirectory is artifacts/bin/<project>/<configuration>/ under UseArtifactsOutput.
        var configurationFolder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var projectFolder = Path.GetDirectoryName(configurationFolder);
        var bin = Path.Combine(layout.Artifacts, "bin");
        if (
            projectFolder is null
            || !string.Equals(
                Path.GetDirectoryName(projectFolder),
                bin,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return [];
        }

        return
        [
            projectFolder,
            Path.Combine(layout.Artifacts, "obj", Path.GetFileName(projectFolder)),
        ];
    }

    private static void TrySetOutputEncoding(Encoding encoding)
    {
        try
        {
            Console.OutputEncoding = encoding;
        }
        catch (IOException)
        {
            // No console attached (for example a detached CI process): keep the default encoding.
        }
    }
}
