using System.Xml.Linq;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// The rules never require positive results (many zones are still empty in M0), so these tests guard against the
/// other way a rule can pass without checking anything: an architecture that did not load what it should.
/// </summary>
public sealed class ArchitectureLoadingTests
{
    [Fact]
    public void Every_product_assembly_is_loaded_for_the_rules()
    {
        var loaded = Product
            .Architecture.Assemblies.Where(assembly => !assembly.IsOnlyReferenced)
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        loaded.ShouldBe(Product.AssemblyOfProject.Values, ignoreOrder: true);
    }

    [Fact]
    public void Every_product_project_of_the_solution_is_covered()
    {
        var solution = XDocument.Load(RepoPaths.Combine("Clicalo.slnx"));
        var productProjects = solution
            .Descendants("Project")
            .Select(project => (string?)project.Attribute("Path") ?? string.Empty)
            .Where(path => path.StartsWith("src/", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension);

        productProjects.ShouldBe(Product.AssemblyOfProject.Keys, ignoreOrder: true);
    }

    [Fact]
    public void Product_types_are_visible_to_the_rules() =>
        Product
            .Architecture.Types.Where(Product.All.Contains)
            .Select(type => type.FullName)
            .ShouldContain(name =>
                string.Equals(name, "Clicalo.App.Program", StringComparison.Ordinal)
            );

    [Fact]
    public void The_violating_fixtures_are_loaded_for_the_negative_tests() =>
        FixtureArchitecture
            .Architecture.Types.Count(type => Zone.IsInNamespace(type, FixtureArchitecture.Root))
            .ShouldBeGreaterThan(10);
}
