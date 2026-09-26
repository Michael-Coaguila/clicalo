namespace Clicalo.Build;

/// <summary>What a captured command wrote and how it ended.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Everything written to stdout.</param>
/// <param name="StandardError">Everything written to stderr.</param>
internal sealed record CommandOutput(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Both streams, stdout first, for reports.</summary>
    public string Combined =>
        string.IsNullOrEmpty(StandardError) ? StandardOutput
        : string.IsNullOrEmpty(StandardOutput) ? StandardError
        : StandardOutput.TrimEnd() + "\n" + StandardError;
}
