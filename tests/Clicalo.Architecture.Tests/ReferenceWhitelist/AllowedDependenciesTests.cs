using System.Collections.Immutable;
using System.Xml.Linq;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests.ReferenceWhitelist;

/// <summary>
/// architecture/allowed-dependencies.json stays true to the repository: every project is declared once in its area,
/// every name it mentions exists, and the product graph it allows keeps the layering of blueprint §4.2.
/// </summary>
public sealed class AllowedDependenciesTests
{
    private static readonly AllowedDependencies Policy =
        ArchitectureDocuments.AllowedDependencies();

    private static readonly ImmutableArray<string> SolutionProjects =
    [
        .. XDocument
            .Load(RepoPaths.Combine("Clicalo.slnx"))
            .Descendants("Project")
            .Select(project => (string?)project.Attribute("Path") ?? string.Empty),
    ];

    [Fact]
    [Trait("Req", "NFR-014")]
    public void Every_project_of_the_solution_is_declared_in_its_area()
    {
        var declared = SolutionProjects.ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => path.Split('/')[0],
            StringComparer.Ordinal
        );

        Policy.Projects.Keys.ShouldBe(declared.Keys, ignoreOrder: true);
        foreach (var (name, entry) in Policy.Projects)
        {
            entry.Area.ShouldBe(declared[name], customMessage: name);
        }
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void Every_referenced_project_is_declared()
    {
        var unknown = Policy
            .Projects.Values.SelectMany(entry => entry.ProjectReferences)
            .Concat(Policy.AnalyzerProjects.Projects)
            .Where(name => !Policy.Projects.ContainsKey(name))
            .ToList();

        unknown.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void Every_package_is_versioned_centrally_and_global_packages_match_Directory_Packages_props()
    {
        var packages = XDocument.Load(RepoPaths.Combine("Directory.Packages.props"));
        string[] Items(string itemType) =>
            [
                .. packages
                    .Descendants(itemType)
                    .Select(item => (string?)item.Attribute("Include") ?? string.Empty),
            ];
        var versioned = Items("PackageVersion")
            .Concat(Items("GlobalPackageReference"))
            .ToHashSet(StringComparer.Ordinal);

        Policy
            .Projects.Values.SelectMany(entry => entry.PackageReferences)
            .Where(package => !versioned.Contains(package))
            .ShouldBeEmpty();
        Policy.GlobalPackages.Packages.ShouldBe(Items("GlobalPackageReference"), ignoreOrder: true);
    }

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Product_projects_only_reference_product_projects_and_never_the_composition_root()
    {
        foreach (var (name, entry) in ProductEntries())
        {
            entry.ProjectReferences.ShouldAllBe(
                reference => IsProduct(reference),
                customMessage: name
            );
            entry.ProjectReferences.ShouldNotContain(
                r => string.Equals(r, "Clicalo.App", StringComparison.Ordinal),
                customMessage: name
            );
        }

        Policy
            .Projects.Where(pair =>
                pair.Value.ProjectReferences.Contains("Clicalo.App", StringComparer.Ordinal)
            )
            .Select(pair => pair.Key)
            .ShouldBe(["Clicalo.Architecture.Tests"]);
    }

    [Fact]
    [Trait("Req", "NFR-012")]
    public void The_product_graph_is_acyclic_and_declares_what_flows_transitively()
    {
        var matrix = new ModuleMatrix(
            "Clicalo",
            ProductEntries()
                .Select(pair => (pair.Key, (IEnumerable<string>)pair.Value.ProjectReferences))
        );
        matrix.Errors.ShouldBeEmpty();

        foreach (var (name, entry) in ProductEntries())
        {
            // Project references flow transitively, so each project declares every product assembly it can see.
            matrix.Reachable[name].SetEquals(entry.ProjectReferences).ShouldBeTrue(name);
        }
    }

    [Fact]
    public void Only_the_WPF_projects_may_use_WPF()
    {
        // The product's WPF layers, plus the Windows test helpers that render WPF visuals for snapshot tests.
        Policy
            .Projects.Where(pair => pair.Value.FrameworkReferences?.Length > 0)
            .Select(pair => pair.Key)
            .ShouldBe(
                ["Clicalo.App", "Clicalo.UI.Wpf", "Clicalo.TestKit.Windows"],
                ignoreOrder: true
            );
    }

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Pure_layers_target_a_portable_framework()
    {
        foreach (
            var project in new[] { "Clicalo.Domain", "Clicalo.Application", "Clicalo.Presentation" }
        )
        {
            Policy.Projects[project].Platform.ShouldBe("portable", customMessage: project);
        }
    }

    [Fact]
    public void No_project_declares_raw_assembly_references() =>
        Policy.Projects.Values.Where(entry => entry.AssemblyReferences?.Length > 0).ShouldBeEmpty();

    private static IEnumerable<KeyValuePair<string, ProjectEntry>> ProductEntries() =>
        Policy.Projects.Where(pair => IsProduct(pair.Key));

    private static bool IsProduct(string project) =>
        string.Equals(
            Policy.Projects.GetValueOrDefault(project)?.Area,
            "src",
            StringComparison.Ordinal
        );
}
