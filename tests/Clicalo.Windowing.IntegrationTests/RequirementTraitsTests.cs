using Clicalo.TestKit.Requirements;

namespace Clicalo.Windowing.IntegrationTests;

/// <summary>Every <c>[Trait("Req", …)]</c> of this project names a requirement of the catalog.</summary>
public sealed class RequirementTraitsTests
{
    [Fact]
    public void Every_requirement_trait_of_this_assembly_names_a_catalog_requirement() =>
        RequirementTraits
            .FindUnknown(typeof(RequirementTraitsTests).Assembly, RequirementCatalog.Default)
            .ShouldBeEmpty();
}
