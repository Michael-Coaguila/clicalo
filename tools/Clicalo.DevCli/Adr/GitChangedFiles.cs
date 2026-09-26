using System.ComponentModel;
using System.Diagnostics;

namespace Clicalo.DevCli.Adr;

/// <summary>Files changed between the merge base of a ref and <c>HEAD</c>, as <c>git diff ref...HEAD</c> lists them.</summary>
internal static class GitChangedFiles
{
    public static bool TryList(
        string root,
        string baseRef,
        out List<string> changed,
        out string problem
    )
    {
        changed = [];
        problem = string.Empty;
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Unquoted UTF-8 paths; --no-renames lists both the old and the new path of a moved file.
        string[] arguments =
        [
            "-c",
            "core.quotePath=false",
            "diff",
            "--name-only",
            "--no-renames",
            baseRef + "...HEAD",
        ];
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var git =
                Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
            var errors = git.StandardError.ReadToEndAsync();
            var lines = git.StandardOutput.ReadToEnd();
            git.WaitForExit();
            if (git.ExitCode != 0)
            {
                problem = "git diff " + baseRef + "...HEAD failed: " + errors.Result.Trim();
                return false;
            }

            changed =
            [
                .. lines.Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
            ];
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            problem = "git could not be started: " + ex.Message;
            return false;
        }
    }
}
