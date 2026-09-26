using ArchUnitNET.xUnitV3;
using Clicalo.Architecture.Tests.Support;

namespace Clicalo.Architecture.Tests;

/// <summary>Layer rules of blueprint §4.1–§4.2 on the compiled product (mechanism 2).</summary>
public sealed class LayerRulesTests
{
    public static TheoryData<string> ProductProjects => [.. Product.AssemblyOfProject.Keys];

    [Theory]
    [MemberData(nameof(ProductProjects))]
    [Trait("Req", "NFR-012")]
    public void Product_types_only_use_the_product_assemblies_their_project_may_reference(
        string project
    ) => ProductRules.Layer(project).Check(Product.Architecture);

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Domain_uses_only_the_base_class_library() =>
        ProductRules.DomainUsesOnlyTheBaseClassLibrary.Check(Product.Architecture);

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Domain_has_no_file_network_registry_or_ui_access() =>
        ProductRules.DomainHasNoOsAccess.Check(Product.Architecture);

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Application_does_not_use_the_ui_framework() =>
        ProductRules.ApplicationHasNoUiFramework.Check(Product.Architecture);

    [Fact]
    [Trait("Req", "NFR-012")]
    public void Presentation_does_not_use_System_Windows() =>
        ProductRules.PresentationHasNoUiFramework.Check(Product.Architecture);

    [Fact]
    public void UI_Wpf_reaches_Application_only_through_its_ports() =>
        ProductRules.UiWpfUsesOnlyUiPortsOfApplication.Check(Product.Architecture);

    [Fact]
    public void UI_Wpf_uses_Win32_only_in_Windowing_and_Pointer() =>
        ProductRules.UiWpfUsesCsWin32OnlyInWindowingAndPointer.Check(Product.Architecture);

    [Fact]
    public void Infrastructure_uses_only_the_Trust_module_of_Platform_Core() =>
        ProductRules.InfrastructureUsesOnlyTrustOfPlatformCore.Check(Product.Architecture);

    [Fact]
    public void Launcher_uses_only_the_Trust_module_of_Platform_Core() =>
        ProductRules.LauncherUsesOnlyTrustOfPlatformCore.Check(Product.Architecture);

    [Fact]
    public void Nothing_depends_on_the_composition_root() =>
        ProductRules.NothingDependsOnTheCompositionRoot.Check(Product.Architecture);

    [Fact]
    [Trait("Req", "NFR-012")]
    public void A_forbidden_layer_dependency_fails_the_rule()
    {
        var rule = DependencyRules.OnlyDependOn(
            FixtureArchitecture.Namespace("Layers.Domain"),
            FixtureArchitecture.Namespace("Layers"),
            [FixtureArchitecture.Namespace("Layers.Application")],
            "negative test"
        );

        Should.Throw<FailedArchRuleException>(() => rule.Check(FixtureArchitecture.Architecture));
        var violations = Rule.Violations(rule, FixtureArchitecture.Architecture);
        violations.ShouldContain(
            FixtureArchitecture.Name("Layers.Domain.LeakyAggregate"),
            Case.Sensitive
        );
        violations.ShouldContain(
            "depends on " + FixtureArchitecture.Name("Layers.Infrastructure.Repository"),
            Case.Sensitive
        );
        violations.ShouldNotContain("CleanValue", Case.Sensitive);
    }

    [Fact]
    public void An_allowed_layer_dependency_passes_the_rule() =>
        DependencyRules
            .OnlyDependOn(
                FixtureArchitecture.Namespace("Layers.Domain"),
                FixtureArchitecture.Namespace("Layers"),
                [FixtureArchitecture.Namespace("Layers.Infrastructure")],
                "negative test control"
            )
            .Check(FixtureArchitecture.Architecture);

    [Fact]
    [Trait("Req", "NFR-012")]
    public void A_view_model_that_uses_WPF_fails_the_Presentation_rule_but_ICommand_does_not()
    {
        var rule = DependencyRules.NotDependOn(
            FixtureArchitecture.Namespace("Layers.Presentation"),
            ProductRules.UiFramework.Except(ProductRules.PortableCommand),
            "negative test"
        );

        var violations = Rule.Violations(rule, FixtureArchitecture.Architecture);
        violations.ShouldContain("WindowAwareViewModel", Case.Sensitive);
        violations.ShouldContain("depends on System.Windows.Visibility", Case.Sensitive);
        violations.ShouldNotContain("CommandViewModel", Case.Sensitive);
    }
}
