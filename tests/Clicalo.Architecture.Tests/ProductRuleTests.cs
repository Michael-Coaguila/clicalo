using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Clicalo.Architecture.Tests.Support;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// Structural side of the product rules R4 and R7 (blueprint §4.4, mechanism 6). The behavioural side (apply every
/// command to generated documents) lands with DocumentStore in M2–M4; these rules are active from M0 so no command
/// can be added outside the registries.
/// </summary>
public sealed class ProductRuleTests
{
    private static readonly Scope Fixture = Scope.Fixture("ProductRules");

    private static readonly DestructiveOperations Destructive =
        ArchitectureDocuments.DestructiveOperations();

    private static readonly string[] Commands = [.. Destructive.Commands.Select(c => c.Name)];

    private static readonly string[] UseCases = [.. Destructive.UseCases.Select(c => c.Name)];

    private static readonly string[] Exemptions =
    [
        .. ArchitectureDocuments.UndoExemptions().Exemptions.Select(e => e.Command),
    ];

    [Fact]
    [Trait("Req", "REG-04")]
    public void Destructive_commands_are_exactly_the_closed_list() =>
        ProductRules
            .DestructiveCommandsAreTheClosedList(Scope.Product, Commands)
            .Check(Product.Architecture);

    [Fact]
    [Trait("Req", "REG-04")]
    public void An_unlisted_or_unmarked_destructive_command_fails_the_rule()
    {
        var violations = FixtureViolations(
            ProductRules.DestructiveCommandsAreTheClosedList(Fixture, Commands)
        );

        violations.ShouldContain(
            "PurgeEverything implements IDestructiveCommand but is not in",
            Case.Sensitive
        );
        violations.ShouldContain(
            "DeleteProfile is in destructive-operations.json but does not implement",
            Case.Sensitive
        );
        violations.ShouldNotContain("DeleteShortcut", Case.Sensitive);
    }

    [Fact]
    [Trait("Req", "REG-04")]
    public void Destructive_use_cases_are_exactly_the_closed_list() =>
        ProductRules
            .DestructiveUseCasesAreTheClosedList(Scope.Product, UseCases)
            .Check(Product.Architecture);

    [Fact]
    [Trait("Req", "REG-04")]
    public void An_unlisted_or_unmarked_destructive_use_case_fails_the_rule()
    {
        var violations = FixtureViolations(
            ProductRules.DestructiveUseCasesAreTheClosedList(Fixture, UseCases)
        );

        violations.ShouldContain(
            "WipeDiskUseCase is marked [Destructive] but is not in",
            Case.Sensitive
        );
        violations.ShouldContain(
            "RollbackVersionUseCase is in destructive-operations.json but is not marked",
            Case.Sensitive
        );
        violations.ShouldNotContain("UninstallSystemComponentUseCase", Case.Sensitive);
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void Undo_exemptions_name_document_commands() =>
        ProductRules
            .UndoExemptionsNameDocumentCommands(Scope.Product, Exemptions)
            .Check(Product.Architecture);

    [Fact]
    [Trait("Req", "REG-07")]
    public void An_undo_exemption_that_is_not_a_document_command_fails_the_rule()
    {
        var violations = FixtureViolations(
            ProductRules.UndoExemptionsNameDocumentCommands(Fixture, Exemptions)
        );

        violations.ShouldContain(
            "FinishOnboarding is in undo-exemptions.json but does not implement",
            Case.Sensitive
        );
        violations.ShouldNotContain("RecordUsage", Case.Sensitive);
    }

    private static string FixtureViolations(IArchRule rule)
    {
        rule.HasNoViolations(FixtureArchitecture.Architecture).ShouldBeFalse();
        return Rule.Violations(rule, FixtureArchitecture.Architecture);
    }
}
