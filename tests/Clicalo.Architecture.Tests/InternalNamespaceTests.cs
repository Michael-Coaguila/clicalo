using ArchUnitNET.xUnitV3;
using Clicalo.Architecture.Tests.Support;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// The public API of a module is the public types at its root; details are internal or live in <c>.Internal</c>,
/// which no other module may use (blueprint §4.3, §4.4).
/// </summary>
public sealed class InternalNamespaceTests
{
    [Fact]
    [Trait("Req", "NFR-012")]
    public void No_module_uses_the_Internal_namespace_of_another_module() =>
        ProductRules.ModulesKeepTheirInternalsPrivate.Check(Product.Architecture);

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Using_the_internals_of_another_module_fails_the_rule()
    {
        var rule = DependencyRules.NotUseOtherModulesInternals(
            FixtureArchitecture.Namespace("Internals"),
            "negative test"
        );

        rule.HasNoViolations(FixtureArchitecture.Architecture).ShouldBeFalse();
        var violations = Rule.Violations(rule, FixtureArchitecture.Architecture);
        violations.ShouldContain("Finder", Case.Sensitive);
        violations.ShouldContain(
            "internal to " + FixtureArchitecture.Name("Internals.Domain.Library"),
            Case.Sensitive
        );
        violations.ShouldNotContain("LibraryAggregate", Case.Sensitive);
    }
}
