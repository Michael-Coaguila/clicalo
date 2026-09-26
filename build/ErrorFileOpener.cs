using System.ComponentModel;
using SimpleExec;

namespace Clicalo.Build;

/// <summary>
/// Opens the failure report in the VS Code window that ran <c>cl</c> (blueprint §13), so a touch or voice
/// user lands on the errors without navigating. Never in CI; <c>CLICALO_OPEN_ERRORS=0</c> turns it off.
/// </summary>
internal static class ErrorFileOpener
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>Opens <paramref name="path"/> when running inside the VS Code terminal; failures are ignored.</summary>
    public static async Task TryOpenAsync(string path)
    {
        if (!ShouldOpen())
        {
            return;
        }

        using var cancellation = new CancellationTokenSource(Timeout);
        try
        {
            // `code` is a .cmd shim on Windows, so it goes through cmd.exe. Only cmd.exe is cancelled on
            // timeout: an editor that was starting up must not be killed.
            await Command.RunAsync(
                "cmd.exe",
                ["/d", "/c", "code", "--reuse-window", path],
                noEcho: true,
                handleExitCode: _ => true,
                cancellationIgnoresProcessTree: true,
                ct: cancellation.Token
            );
        }
        catch (Exception exception) when (exception is Win32Exception or OperationCanceledException)
        {
            // Opening the report is a convenience; the final line already says where it is.
        }
    }

    private static bool ShouldOpen() =>
        OperatingSystem.IsWindows()
        && IsSet("TERM_PROGRAM", "vscode")
        && !IsSet("CI", "true")
        && !IsSet("GITHUB_ACTIONS", "true")
        && !IsSet("CLICALO_OPEN_ERRORS", "0");

    private static bool IsSet(string variable, string value) =>
        string.Equals(
            Environment.GetEnvironmentVariable(variable),
            value,
            StringComparison.OrdinalIgnoreCase
        );
}
