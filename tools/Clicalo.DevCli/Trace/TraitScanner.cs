using System.Text.RegularExpressions;

namespace Clicalo.DevCli.Trace;

/// <summary>
/// Finds the requirement traits of a C# source file by reading its text: no build and no test run are needed, so the
/// result is the same on every machine. A trait before a method belongs to that test; a trait before a type covers
/// every test of the type. Lines that are comments are skipped (the documentation shows the notation too).
/// </summary>
internal static partial class TraitScanner
{
    /// <summary>How far below a trait its method or type is looked for (other attributes and their arguments).</summary>
    private const int DeclarationWindow = 80;

    private static readonly string[] DeclarationStarts =
    [
        "public ",
        "internal ",
        "private ",
        "protected ",
        "static ",
        "sealed ",
        "abstract ",
        "partial ",
        "async ",
        "file ",
    ];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "public",
        "internal",
        "private",
        "protected",
        "static",
        "async",
        "override",
        "virtual",
        "new",
        "unsafe",
        "extern",
    };

    /// <summary>The traits of <paramref name="source"/>, in the order they appear.</summary>
    /// <param name="file">The path to report, relative to the repository root.</param>
    /// <param name="source">The text of the file.</param>
    public static IReadOnlyList<TestReference> Scan(string file, string source)
    {
        var references = new List<TestReference>();
        var lines = source.Split('\n');
        string? topLevelType = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            // File-scoped namespaces: the test class starts at the first column, its nested helpers do not.
            if (
                line.Length > 0
                && !char.IsWhiteSpace(line[0])
                && TypePattern().Match(line) is { Success: true } type
            )
            {
                topLevelType = type.Groups["name"].Value;
            }

            foreach (Match trait in TraitPattern().Matches(line))
            {
                references.Add(
                    new TestReference(
                        trait.Groups["id"].Value,
                        file,
                        i + 1,
                        Owner(lines, i, topLevelType)
                    )
                );
            }
        }

        return references;
    }

    private static string Owner(string[] lines, int traitLine, string? topLevelType)
    {
        var last = Math.Min(lines.Length, traitLine + 1 + DeclarationWindow);
        for (var i = traitLine + 1; i < last; i++)
        {
            var text = lines[i].TrimStart();
            if (!DeclarationStarts.Any(start => text.StartsWith(start, StringComparison.Ordinal)))
            {
                continue;
            }

            if (TypePattern().Match(text) is { Success: true } type)
            {
                return type.Groups["name"].Value;
            }

            foreach (Match method in MethodPattern().Matches(text))
            {
                var name = method.Groups["name"].Value;
                if (!Keywords.Contains(name))
                {
                    return topLevelType is null ? name : topLevelType + "." + name;
                }
            }

            break;
        }

        return topLevelType ?? "?";
    }

    [GeneratedRegex(
        """Trait\(\s*"Req"\s*,\s*"(?<id>[^"]*)"\s*\)""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex TraitPattern();

    [GeneratedRegex(
        @"\b(?:record\s+(?:class|struct)|class|struct|interface|record)\s+(?<name>[A-Za-z_]\w*)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex TypePattern();

    [GeneratedRegex(
        @"(?<name>[A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex MethodPattern();
}
