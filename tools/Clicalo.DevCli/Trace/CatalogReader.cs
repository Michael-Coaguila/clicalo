using System.Text.RegularExpressions;

namespace Clicalo.DevCli.Trace;

/// <summary>
/// Reads the declarations of <c>docs/requirements/catalog.md</c>, in the order of the document: requirements
/// (<c>- **EJE-003 · MUST · Título.** …</c>, and the two-digit rules <c>- **REG-01 · MUST · …</c>) and the edge cases
/// of §4 (<c>- **EC-EJE-10.** …</c>). It is the same shape <c>Clicalo.TestKit</c> checks the traits against.
/// </summary>
internal static partial class CatalogReader
{
    /// <summary>How a requirement put off by the user starts its text (catalog §6.2, D8).</summary>
    private const string DeferredMark = "**Aplazado";

    private const int EdgeCaseTitleLength = 70;

    /// <summary>The entries of the catalog text <paramref name="markdown"/>.</summary>
    public static IReadOnlyList<CatalogEntry> Read(string markdown)
    {
        var entries = new List<CatalogEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = markdown.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var entry = Requirement(line, i + 1) ?? EdgeCase(line, i + 1);
            if (entry is not null && seen.Add(entry.Id))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static CatalogEntry? Requirement(string line, int number)
    {
        var match = RequirementPattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        RequirementPriority? priority = match.Groups["priority"].Value.Trim() switch
        {
            "MUST" => RequirementPriority.Must,
            "SHOULD" => RequirementPriority.Should,
            "COULD" => RequirementPriority.Could,
            "Retirado" => RequirementPriority.Retired,
            _ => null,
        };
        return priority is null
            ? null
            : new CatalogEntry(
                match.Groups["id"].Value,
                priority.Value,
                match.Groups["title"].Value.Trim().TrimEnd('.'),
                line.Contains(DeferredMark, StringComparison.Ordinal),
                number
            );
    }

    private static CatalogEntry? EdgeCase(string line, int number)
    {
        var match = EdgeCasePattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var text = match.Groups["text"].Value.Trim();
        var title =
            text.Length <= EdgeCaseTitleLength ? text : text[..EdgeCaseTitleLength].TrimEnd() + "…";
        return new CatalogEntry(
            match.Groups["id"].Value,
            RequirementPriority.EdgeCase,
            title.TrimEnd('.'),
            Deferred: false,
            number
        );
    }

    [GeneratedRegex(
        @"^- \*\*(?<id>[A-Z]{2,4}-\d{2,3}) · (?<priority>[^·*]+) · (?<title>.+?)\*\*",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex RequirementPattern();

    [GeneratedRegex(
        @"^- \*\*(?<id>EC-[A-Z]{2,4}-\d{2})\.\*\*(?<text>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex EdgeCasePattern();
}
