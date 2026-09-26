using Clicalo.Platform.IntegrationTests.Injection;
using Clicalo.TestKit.Requirements;

namespace Clicalo.Platform.IntegrationTests.TestKit;

/// <summary>The <c>[Trait("Req", …)]</c> convention (Clicalo.TestKit): every reference names a catalog requirement.</summary>
public sealed class RequirementTraitsTests
{
    [Fact]
    public void The_catalog_declares_requirements_and_edge_cases()
    {
        var catalog = RequirementCatalog.Default;

        catalog.Ids.Count.ShouldBeGreaterThan(300);
        catalog.Contains("EJE-003").ShouldBeTrue();
        catalog.Contains("NFR-004").ShouldBeTrue();
        catalog.Contains("EC-EJE-10").ShouldBeTrue();
        catalog.Contains("EJE-999").ShouldBeFalse();
        catalog.Contains("DIS-34").ShouldBeFalse("Divergences are not requirements.");
    }

    [Fact]
    public void Every_requirement_trait_of_this_assembly_names_a_catalog_requirement() =>
        RequirementTraits
            .FindUnknown(typeof(RequirementTraitsTests).Assembly, RequirementCatalog.Default)
            .ShouldBeEmpty();

    [Fact]
    public void Requirement_traits_are_found_on_test_methods()
    {
        var references = RequirementTraits.FindIn(typeof(RequirementTraitsTests).Assembly);

        references.ShouldContain(
            new RequirementReference(
                "EJE-003",
                typeof(KeyboardInjectionTests).FullName
                    + "."
                    + nameof(
                        KeyboardInjectionTests.Ctrl_A_is_pressed_in_order_and_released_in_reverse_order
                    )
            )
        );
        references.ShouldNotContain(reference => reference.Id == RequirementTraits.Desktop);
    }
}
