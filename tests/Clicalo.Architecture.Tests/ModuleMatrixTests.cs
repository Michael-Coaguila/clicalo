using ArchUnitNET.xUnitV3;
using Clicalo.Architecture.Tests.Support;

namespace Clicalo.Architecture.Tests;

/// <summary>The Domain module matrix of blueprint §4.3 (architecture/domain-modules.json).</summary>
public sealed class ModuleMatrixTests
{
    private static readonly ModuleMatrix FixtureMatrix = new(
        FixtureArchitecture.Name("Modules.Domain"),
        [("Keys", []), ("Library", ["Keys"]), ("Sharing.Profiles", ["Library"])]
    );

    /// <summary>The table of blueprint §4.3, row by row.</summary>
    private static readonly Dictionary<string, string[]> Blueprint = new(StringComparer.Ordinal)
    {
        ["Primitives"] = [],
        ["Geometry"] = [],
        ["Messages"] = [],
        ["Errors"] = [],
        ["Privacy"] = [],
        ["Keys"] = ["Primitives"],
        ["Catalog"] = ["Keys", "Primitives"],
        ["Library"] = ["Keys", "Catalog", "Primitives", "Messages", "Privacy"],
        ["Settings"] = ["Primitives"],
        ["ProfileResolution"] = ["Library", "Settings"],
        ["Frequents"] = ["Library"],
        ["Duplicates"] = ["Library", "Keys"],
        ["Search"] = ["Library", "Catalog"],
        ["KeySafety"] = ["Keys"],
        ["StickyModifiers"] = ["Keys", "KeySafety"],
        ["Touch"] = ["Geometry", "Primitives"],
        ["PanelLayout"] = ["Settings", "Geometry"],
        ["VoiceNumbering"] = ["PanelLayout"],
        ["Icons"] = ["Catalog", "Keys"],
        ["Dimming"] = ["Settings", "Primitives"],
        ["Interaction"] = ["Messages", "Primitives", "Library"],
        ["Execution"] =
        [
            "Library",
            "KeySafety",
            "StickyModifiers",
            "Touch",
            "Settings",
            "Messages",
            "Errors",
            "Geometry",
        ],
        ["Templates"] = ["Library", "Catalog", "Keys"],
        ["Sharing"] = ["Library", "Catalog", "Keys"],
    };

    [Fact]
    [Trait("Req", "NFR-012")]
    public void The_domain_module_matrix_is_valid_and_acyclic() =>
        ModuleMatrix.From(ArchitectureDocuments.DomainModules()).Errors.ShouldBeEmpty();

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Domain_types_live_in_declared_modules_and_only_use_the_modules_the_matrix_allows()
    {
        var document = ArchitectureDocuments.DomainModules();
        document.RootNamespace.ShouldBe("Clicalo.Domain");
        ModuleMatrix
            .From(document)
            .Rule(Product.Domain, "the matrix of §4.3 is acyclic and closed")
            .Check(Product.Architecture);
    }

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Every_row_that_differs_from_the_blueprint_table_states_its_deviation()
    {
        var modules = ArchitectureDocuments.DomainModules().Modules;

        Blueprint.Keys.Except(modules.Select(m => m.Name), StringComparer.Ordinal).ShouldBeEmpty();
        foreach (var module in modules)
        {
            var same =
                Blueprint.TryGetValue(module.Name, out var dependsOn)
                && dependsOn.ToHashSet(StringComparer.Ordinal).SetEquals(module.DependsOn);
            (module.Deviation is null).ShouldBe(same, module.Name);
        }
    }

    [Fact]
    public void The_matrix_follows_dependencies_transitively()
    {
        var matrix = ModuleMatrix.From(ArchitectureDocuments.DomainModules());

        matrix.Allows("ProfileResolution", "Primitives").ShouldBeTrue();
        matrix.Allows("Execution", "Keys").ShouldBeTrue();
        matrix.Allows("Keys", "Library").ShouldBeFalse();
        matrix.Allows("Primitives", "Keys").ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "NFR-012")]
    public void An_undeclared_module_edge_and_a_type_outside_every_module_fail_the_rule()
    {
        var rule = FixtureMatrix.Rule(FixtureArchitecture.Namespace("Modules"), "negative test");

        rule.HasNoViolations(FixtureArchitecture.Architecture).ShouldBeFalse();
        var violations = Rule.Violations(rule, FixtureArchitecture.Architecture);
        violations.ShouldContain(
            "Keys -> Library (uses " + FixtureArchitecture.Name("Modules.Domain.Library.Shortcut"),
            Case.Sensitive
        );
        violations.ShouldContain("Orphan", Case.Sensitive);
        violations.ShouldContain("is in no declared module", Case.Sensitive);
        violations.ShouldNotContain("Importer", Case.Sensitive);
        violations.ShouldNotContain("Library -> Keys", Case.Sensitive);
    }

    [Fact]
    public void A_matrix_with_a_cycle_an_unknown_module_or_a_self_edge_is_rejected()
    {
        var matrix = new ModuleMatrix(
            "Root",
            [("A", ["B"]), ("B", ["C"]), ("C", ["A"]), ("D", ["Missing", "D"]), ("A", [])]
        );

        matrix.Errors.ShouldContain(error =>
            error.StartsWith("Cycle: A -> B -> C -> A", StringComparison.Ordinal)
        );
        matrix.Errors.ShouldContain(error =>
            string.Equals(
                error,
                "Module D depends on the undeclared module Missing.",
                StringComparison.Ordinal
            )
        );
        matrix.Errors.ShouldContain(error =>
            string.Equals(error, "Module D depends on itself.", StringComparison.Ordinal)
        );
        matrix.Errors.ShouldContain(error =>
            string.Equals(error, "Module A is declared twice.", StringComparison.Ordinal)
        );
    }
}
