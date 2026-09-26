namespace Clicalo.Build;

/// <summary>
/// How much <c>cl</c> writes. Locally the console stays quiet for Narrator (step lines, errors and the final
/// line); CI logs and <c>--verbose</c> also show every command and MSBuild's per-project lines.
/// The checks themselves are identical in both modes.
/// </summary>
/// <param name="Ci">Running in continuous integration.</param>
/// <param name="GitHubActions">Running in GitHub Actions: steps become collapsible log groups.</param>
/// <param name="Verbose">The maintainer asked for detail with <c>--verbose</c>.</param>
internal sealed record OutputMode(bool Ci, bool GitHubActions, bool Verbose)
{
    /// <summary>Whether each command line is echoed before it runs.</summary>
    public bool EchoCommands => Ci || Verbose;

    /// <summary>MSBuild console verbosity.</summary>
    public string MsBuildVerbosity => Ci || Verbose ? "minimal" : "quiet";

    /// <summary>Reads the environment (<c>CI</c>, <c>GITHUB_ACTIONS</c>).</summary>
    public static OutputMode Detect(bool verbose)
    {
        var gitHubActions = IsTrue("GITHUB_ACTIONS");
        return new OutputMode(IsTrue("CI") || gitHubActions, gitHubActions, verbose);
    }

    private static bool IsTrue(string variable) =>
        string.Equals(
            Environment.GetEnvironmentVariable(variable),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
}
