using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Clicalo.Build;

/// <summary>
/// Reads the errors-only file log that <c>cl</c> asks MSBuild to write (<c>-flp1:ErrorsOnly</c>) and turns
/// it into a numbered, deduplicated list with links to the exact line.
/// </summary>
internal static partial class MsBuildErrorLog
{
    /// <summary>How many errors the report lists before pointing to the full log.</summary>
    public const int MaxListed = 50;

    /// <summary>Parses the lines of an errors-only log; unrecognized lines continue the previous error.</summary>
    public static IReadOnlyList<MsBuildDiagnostic> Parse(IEnumerable<string> lines)
    {
        var diagnostics = new List<MsBuildDiagnostic>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        MsBuildDiagnostic? pending = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var match = ErrorLine().Match(line);
            if (match.Success)
            {
                Flush();
                pending = new MsBuildDiagnostic(
                    match.Groups["origin"].Value.Trim(),
                    ParseNumber(match.Groups["line"]),
                    ParseNumber(match.Groups["column"]),
                    match.Groups["code"].Success ? match.Groups["code"].Value : null,
                    match.Groups["message"].Value.Trim(),
                    match.Groups["project"].Success ? match.Groups["project"].Value.Trim() : null
                );
            }
            else if (pending is not null)
            {
                pending = pending with { Message = pending.Message + " " + line };
            }
            else
            {
                pending = new MsBuildDiagnostic("MSBuild", null, null, null, line, null);
            }
        }

        Flush();
        return diagnostics;

        void Flush()
        {
            if (pending is null)
            {
                return;
            }

            var key = string.Create(
                CultureInfo.InvariantCulture,
                $"{pending.Origin}|{pending.Line}|{pending.Column}|{pending.Code}|{pending.Message}"
            );
            if (seen.Add(key))
            {
                diagnostics.Add(pending);
            }

            pending = null;
        }
    }

    /// <summary>Reads and parses <paramref name="path"/>; a missing file means no errors were logged.</summary>
    public static IReadOnlyList<MsBuildDiagnostic> Load(string path) =>
        File.Exists(path) ? Parse(File.ReadLines(path, Encoding.UTF8)) : [];

    /// <summary>
    /// Renders <paramref name="diagnostics"/> as a numbered Markdown list. File origins become links relative
    /// to <paramref name="reportDirectory"/> that open at the reported line.
    /// </summary>
    public static string Render(
        IReadOnlyList<MsBuildDiagnostic> diagnostics,
        RepoLayout layout,
        string reportDirectory
    )
    {
        var list = new StringBuilder();
        var number = 0;
        foreach (var diagnostic in diagnostics.Take(MaxListed))
        {
            number++;
            list.Append(number.ToString(CultureInfo.InvariantCulture)).Append(". ");
            list.Append(DescribeOrigin(diagnostic, layout, reportDirectory)).Append(": ");
            if (diagnostic.Code is not null)
            {
                list.Append(diagnostic.Code).Append(' ');
            }

            list.Append(Markdown.Text(diagnostic.Message));
            if (ProjectWorthNaming(diagnostic) is { } project)
            {
                list.Append(" (")
                    .Append(Markdown.Text(Path.GetFileNameWithoutExtension(project)))
                    .Append(')');
            }

            list.Append('\n');
        }

        if (diagnostics.Count > MaxListed)
        {
            list.Append('\n')
                .Append(Messages.TruncatedItems(MaxListed, diagnostics.Count))
                .Append('\n');
        }

        return list.ToString();
    }

    private static string DescribeOrigin(
        MsBuildDiagnostic diagnostic,
        RepoLayout layout,
        string reportDirectory
    )
    {
        if (!Path.IsPathFullyQualified(diagnostic.Origin))
        {
            return Markdown.Text(diagnostic.Origin);
        }

        var shown = IsUnder(diagnostic.Origin, layout.Root)
            ? layout.RelativeForward(diagnostic.Origin)
            : diagnostic.Origin.Replace('\\', '/');
        var text = Messages.LineColumn(shown, diagnostic.Line, diagnostic.Column);
        var destination = Path.GetRelativePath(reportDirectory, diagnostic.Origin);
        if (diagnostic.Line is { } line)
        {
            destination += string.Create(CultureInfo.InvariantCulture, $"#L{line}");
        }

        return Markdown.Link(text, destination);
    }

    /// <summary>
    /// The "[project]" suffix, unless it adds nothing: restore errors are reported on the project file
    /// itself, and solution-level entries only name what was built.
    /// </summary>
    private static string? ProjectWorthNaming(MsBuildDiagnostic diagnostic) =>
        diagnostic.Project is { } project
        && !string.Equals(project, diagnostic.Origin, StringComparison.OrdinalIgnoreCase)
        && Path.GetExtension(project).ToUpperInvariant() is not (".SLN" or ".SLNX" or ".SLNF")
            ? project
            : null;

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(
            root + Path.AltDirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );

    private static int? ParseNumber(Group group) =>
        group.Success
        && int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    // [node:instance>]origin(line,col[,endLine,endCol]): [fatal ]error CODE: message [project]
    // The "7:15>" prefix is how the multi-process file logger tags the project instance that logged the line.
    [GeneratedRegex(
        @"^(\d+(:\d+)?>)?(?<origin>.*?)(?:\((?<line>\d+)(?:,(?<column>\d+))?(?:,\d+,\d+)?\))?\s*:\s*(?:fatal\s+)?error(?:\s+(?<code>[A-Za-z]+[0-9]+[A-Za-z0-9]*))?\s*:\s*(?<message>.*?)(?:\s+\[(?<project>[^\[\]]+)\])?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ErrorLine();
}
