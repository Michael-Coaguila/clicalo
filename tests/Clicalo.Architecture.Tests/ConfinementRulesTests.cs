using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Clicalo.Architecture.Tests.Support;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// Who may use the dangerous capabilities (blueprint §4.4, mechanism 2): ports, the input injector, the foreground
/// control, secret text, the single-writer stores and the IPC server. Each product rule is also run on the violating
/// fixtures of <c>Fixtures.Confinement</c>, which mirror the product namespaces.
/// </summary>
public sealed class ConfinementRulesTests
{
    private static readonly Scope Fixture = Scope.Fixture("Confinement");

    [Fact]
    public void Adapters_implement_only_ports_from_Application_Ports() =>
        ProductRules.AdaptersImplementOnlyPorts(Scope.Product).Check(Product.Architecture);

    [Fact]
    public void An_adapter_of_an_interface_outside_Application_Ports_fails_the_rule()
    {
        var violations = FixtureViolations(ProductRules.AdaptersImplementOnlyPorts(Fixture));

        violations.ShouldContain("MisplacedAdapter", Case.Sensitive);
        violations.ShouldContain(
            "IMisplacedPort, which is not in " + Fixture.Name("Clicalo.Application.Ports"),
            Case.Sensitive
        );
        violations.ShouldNotContain("Foreground.ForegroundControl", Case.Sensitive);
    }

    [Fact]
    [Trait("Req", "SEG-007")]
    public void Only_the_engine_Platform_Windows_and_App_use_the_input_injector() =>
        ProductRules
            .OnlyEnginePlatformAndAppUseTheInputInjector(Scope.Product)
            .Check(Product.Architecture);

    [Fact]
    [Trait("Req", "SEG-007")]
    public void Using_the_input_injector_outside_the_engine_fails_the_rule()
    {
        var violations = FixtureViolations(
            ProductRules.OnlyEnginePlatformAndAppUseTheInputInjector(Fixture)
        );

        violations.ShouldContain("PanelViewModel", Case.Sensitive);
        violations.ShouldContain(
            "depends on " + Fixture.Name("Clicalo.Application.Ports.IInputInjector"),
            Case.Sensitive
        );
        violations.ShouldNotContain("EngineHost", Case.Sensitive);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void Only_Application_Foreground_uses_the_foreground_control() =>
        ProductRules
            .OnlyForegroundUsesTheForegroundControl(Scope.Product)
            .Check(Product.Architecture);

    [Fact]
    [Trait("Req", "REG-01")]
    public void Using_the_foreground_control_outside_the_orchestrator_fails_the_rule()
    {
        var violations = FixtureViolations(
            ProductRules.OnlyForegroundUsesTheForegroundControl(Fixture)
        );

        violations.ShouldContain("PanelViewModel", Case.Sensitive);
        violations.ShouldContain("IForegroundControl", Case.Sensitive);
        violations.ShouldNotContain("ForegroundOrchestrator", Case.Sensitive);
        violations.ShouldNotContain("Windows.Foreground.ForegroundControl", Case.Sensitive);
    }

    [Fact]
    [Trait("Req", "LOG-003")]
    [Trait("Req", "LOG-004")]
    public void Only_execution_and_the_editor_reveal_secret_text() =>
        ProductRules
            .OnlyExecutionAndTheEditorRevealSecrets(Scope.Product)
            .Check(Product.Architecture);

    [Fact]
    [Trait("Req", "LOG-004")]
    public void Revealing_secret_text_elsewhere_fails_the_rule()
    {
        var violations = FixtureViolations(
            ProductRules.OnlyExecutionAndTheEditorRevealSecrets(Fixture)
        );

        violations.ShouldContain("PanelViewModel", Case.Sensitive);
        violations.ShouldContain("WithRevealed", Case.Sensitive);
        violations.ShouldNotContain("TextPlanner", Case.Sensitive);
    }

    [Fact]
    public void Only_the_Surfaces_role_writes_the_session_and_interaction_stores() =>
        ProductRules
            .OnlyTheSurfacesRoleWritesSessionAndInteraction(Scope.Product)
            .Check(Product.Architecture);

    [Fact]
    public void Writing_a_store_from_the_Workspace_role_fails_the_rule_but_reading_does_not()
    {
        var violations = FixtureViolations(
            ProductRules.OnlyTheSurfacesRoleWritesSessionAndInteraction(Fixture)
        );

        violations.ShouldContain("SectionViewModel", Case.Sensitive);
        violations.ShouldContain("SessionStore::Dispatch(System.Int32)", Case.Sensitive);
        violations.ShouldNotContain("StatusViewModel", Case.Sensitive);
        violations.ShouldNotContain("PanelViewModel", Case.Sensitive);
    }

    [Fact]
    public void The_IPC_server_depends_neither_on_the_engine_nor_on_the_foreground() =>
        ProductRules.IpcDoesNotReachEngineOrForeground(Scope.Product).Check(Product.Architecture);

    [Fact]
    public void The_IPC_server_uses_only_IShellNavigator_from_Application() =>
        ProductRules.IpcUsesOnlyTheShellNavigator(Scope.Product).Check(Product.Architecture);

    [Fact]
    public void An_IPC_server_that_reaches_the_engine_fails_both_IPC_rules()
    {
        foreach (
            var rule in new[]
            {
                ProductRules.IpcDoesNotReachEngineOrForeground(Fixture),
                ProductRules.IpcUsesOnlyTheShellNavigator(Fixture),
            }
        )
        {
            var violations = FixtureViolations(rule);
            violations.ShouldContain("LeakyIpcServer", Case.Sensitive);
            violations.ShouldContain(
                "depends on " + Fixture.Name("Clicalo.Application.Engine.EngineHost"),
                Case.Sensitive
            );
            violations.ShouldNotContain("ShowRequestHandler", Case.Sensitive);
        }
    }

    private static string FixtureViolations(IArchRule rule)
    {
        rule.HasNoViolations(FixtureArchitecture.Architecture).ShouldBeFalse();
        return Rule.Violations(rule, FixtureArchitecture.Architecture);
    }
}
