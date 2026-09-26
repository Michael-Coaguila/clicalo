using System.Collections.Immutable;
using System.Globalization;

namespace Clicalo.DevCli.Adr;

/// <summary>
/// <c>adr-check --base &lt;ref&gt;</c>: lists the files changed since the merge base with <c>ref</c> (what a pull request
/// shows) and fails when a sensitive path changed without an ADR (blueprint §13). Output uses the MSBuild format and
/// ends with one line that Narrator can read aloud.
/// </summary>
internal static class AdrCheckCommand
{
    /// <summary>Id of the diagnostic printed for each sensitive change without an ADR.</summary>
    public const string MissingAdrId = "CLCA010";

    public static int Run(string root, string baseRef, TextWriter output)
    {
        ImmutableArray<SensitivePath> sensitive;
        try
        {
            sensitive = SensitivePaths.Parse(
                File.ReadAllText(Path.Combine(root, SensitivePaths.FileName))
            );
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            output.WriteLine("error: " + ex.Message);
            output.WriteLine("adr-check: the sensitive-path registry could not be read.");
            return ExitCodes.Failure;
        }

        if (!GitChangedFiles.TryList(root, baseRef, out var changed, out var problem))
        {
            output.WriteLine("error: " + problem);
            output.WriteLine("adr-check: the changed files could not be listed.");
            return ExitCodes.Failure;
        }

        return Report(AdrCheck.Evaluate(sensitive, changed), changed.Count, output);
    }

    /// <summary>Prints the result and returns the exit code.</summary>
    public static int Report(AdrCheckResult result, int changedCount, TextWriter output)
    {
        if (result.Touched.IsEmpty)
        {
            output.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"adr-check: no sensitive path changed ({changedCount} files)."
                )
            );
            return ExitCodes.Success;
        }

        if (result.AdrChanged)
        {
            output.WriteLine("adr-check: " + Count(result) + " changed with an ADR.");
            return ExitCodes.Success;
        }

        foreach (var touch in result.Touched)
        {
            output.WriteLine(
                touch.File
                    + ": error "
                    + MissingAdrId
                    + ": '"
                    + touch.Path.Glob.Pattern
                    + "' ("
                    + touch.Path.Category
                    + ") needs a new or changed ADR under docs/adr/. "
                    + touch.Path.Reason
            );
        }

        output.WriteLine(
            "adr-check: "
                + Count(result)
                + " changed without an ADR. Add or update docs/adr/NNNN-*.md."
        );
        return ExitCodes.Failure;
    }

    private static string Count(AdrCheckResult result) =>
        Count(result.Touched.Select(static t => t.File).Distinct(StringComparer.Ordinal).Count());

    private static string Count(int touched) =>
        touched == 1
            ? "1 file in a sensitive path"
            : string.Create(CultureInfo.InvariantCulture, $"{touched} files in sensitive paths");
}
