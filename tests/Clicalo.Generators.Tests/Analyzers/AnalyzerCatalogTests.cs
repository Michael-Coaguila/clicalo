using System.Text.RegularExpressions;
using Clicalo.Analyzers;
using Clicalo.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Clicalo.Generators.Tests.Analyzers;

/// <summary>
/// Keeps the rule catalog honest: every id is an error, documented in docs/guides/analyzers.md, declared in the
/// release-tracking file and owned by exactly one analyzer.
/// </summary>
public sealed partial class AnalyzerCatalogTests
{
    private static readonly string[] MilestoneZeroIds =
    [
        "CLC0001",
        "CLC0003",
        "CLC0004",
        "CLC0006",
        "CLC0010",
    ];

    private static readonly DiagnosticAnalyzer[] Analyzers =
    [
        .. typeof(DiagnosticIds)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!),
    ];

    private static IEnumerable<DiagnosticDescriptor> Descriptors =>
        Analyzers.SelectMany(a => a.SupportedDiagnostics);

    [Fact]
    public void Milestone_zero_rules_are_all_present() =>
        Descriptors
            .Select(d => d.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ShouldBe(MilestoneZeroIds);

    [Fact]
    public void Every_rule_is_an_error_enabled_by_default_with_its_help_link() =>
        Descriptors.ShouldAllBe(d =>
            d.DefaultSeverity == DiagnosticSeverity.Error
            && d.IsEnabledByDefault
            && d.HelpLinkUri.EndsWith(
                "analyzers.md#" + d.Id.ToLowerInvariant(),
                StringComparison.Ordinal
            )
        );

    [Fact]
    public void Every_id_belongs_to_one_analyzer_and_one_category()
    {
        foreach (var group in Descriptors.GroupBy(d => d.Id, StringComparer.Ordinal))
        {
            Analyzers
                .Count(a =>
                    a.SupportedDiagnostics.Any(d =>
                        string.Equals(d.Id, group.Key, StringComparison.Ordinal)
                    )
                )
                .ShouldBe(1, group.Key);
            group
                .Select(d => d.Category)
                .Distinct(StringComparer.Ordinal)
                .Count()
                .ShouldBe(1, group.Key);
        }
    }

    [Fact]
    public void Every_id_has_its_section_in_the_guide()
    {
        var guide = File.ReadAllText(RepoPaths.Combine("docs", "guides", "analyzers.md"));

        foreach (var id in MilestoneZeroIds)
        {
            guide.ShouldContain($"<a id=\"{id.ToLowerInvariant()}\"></a>", customMessage: id);
        }
    }

    [Fact]
    public void Every_id_is_declared_in_release_tracking_with_its_category()
    {
        var unshipped = File.ReadAllText(
            RepoPaths.Combine("generators", "Clicalo.Analyzers", "AnalyzerReleases.Unshipped.md")
        );
        var rows = ReleaseRow()
            .Matches(unshipped)
            .ToDictionary(
                m => m.Groups["id"].Value,
                m => m.Groups["category"].Value,
                StringComparer.Ordinal
            );

        foreach (var descriptor in Descriptors)
        {
            rows.ShouldContainKey(descriptor.Id);
            rows[descriptor.Id].ShouldBe(descriptor.Category);
        }
    }

    [GeneratedRegex(
        @"^(?<id>CLC\d{4})\s*\|\s*(?<category>[\w.]+)\s*\|\s*Error\s*\|",
        RegexOptions.Multiline,
        1000
    )]
    private static partial Regex ReleaseRow();
}
