using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace Clicalo.TestKit.Requirements;

/// <summary>
/// The requirement identifiers declared in <c>docs/requirements/catalog.md</c>: requirements
/// (<c>- **EJE-003 · MUST · …</c>, and the two-digit rules <c>- **REG-01 · MUST · …</c>) and edge cases
/// (<c>- **EC-EJE-10.** …</c>).
/// </summary>
public sealed partial class RequirementCatalog
{
    private static readonly Lazy<RequirementCatalog> DefaultCatalog = new(() =>
        Load(RepoPaths.Combine("docs", "requirements", "catalog.md"))
    );

    private RequirementCatalog(FrozenSet<string> ids)
    {
        Ids = ids;
    }

    /// <summary>The repository catalog, parsed once per test run.</summary>
    public static RequirementCatalog Default => DefaultCatalog.Value;

    /// <summary>Every declared identifier (ordinal comparison).</summary>
    public FrozenSet<string> Ids { get; }

    /// <summary>Parses the catalog at <paramref name="path"/>.</summary>
    public static RequirementCatalog Load(string path)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var match = DeclarationPattern().Match(line);
            if (match.Success)
            {
                ids.Add(match.Groups["id"].Value);
            }
        }

        if (ids.Count == 0)
        {
            throw new InvalidDataException(
                "No requirement declarations were found in '" + path + "'."
            );
        }

        return new RequirementCatalog(ids.ToFrozenSet(StringComparer.Ordinal));
    }

    /// <summary>True when <paramref name="id"/> is declared in the catalog.</summary>
    public bool Contains(string id) => Ids.Contains(id);

    [GeneratedRegex(
        @"^- \*\*(?<id>[A-Z]{2,4}-\d{2,3}|EC-[A-Z]{2,4}-\d{2})(?: ·|\.\*\*)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex DeclarationPattern();
}
