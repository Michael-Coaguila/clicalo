using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Clicalo.DevCli.Adr;

/// <summary>
/// The ADR rule of blueprint §13: a change that touches a sensitive path must add or change an ADR
/// (<c>docs/adr/NNNN-*.md</c>, not the template) in the same pull request.
/// </summary>
internal static partial class AdrCheck
{
    /// <summary>The sensitive paths that <paramref name="changed"/> touches, and whether an ADR changed too.</summary>
    public static AdrCheckResult Evaluate(
        ImmutableArray<SensitivePath> sensitive,
        IEnumerable<string> changed
    )
    {
        var files = changed
            .Select(static f => f.Trim().Replace('\\', '/'))
            .Where(static f => f.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        var touched = files
            .SelectMany(file =>
                sensitive.Where(s => s.Glob.IsMatch(file)).Select(s => new AdrTouch(file, s))
            )
            .ToImmutableArray();
        return new AdrCheckResult(touched, files.Any(IsAdr));
    }

    /// <summary>True for an ADR file: <c>docs/adr/NNNN-slug.md</c> other than <c>0000-template.md</c>.</summary>
    public static bool IsAdr(string path)
    {
        var normalized = path.Replace('\\', '/');
        return AdrFile().IsMatch(normalized)
            && !normalized.EndsWith("/0000-template.md", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(
        "^docs/adr/[0-9]{4}-[^/]+\\.md$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex AdrFile();
}
