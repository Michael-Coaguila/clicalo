using Clicalo.Generators.Localization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Clicalo.Generators.Tests.Localization;

/// <summary>
/// The generator only reads data/i18n and the generator profile: editing C# must not run it again (blueprint §8.5,
/// incremental generators). A pipeline that starts reading the compilation fails here.
/// </summary>
public sealed class LocalizationGeneratorIncrementalTests
{
    [Fact]
    public void A_code_edit_leaves_the_input_and_the_generated_sources_cached()
    {
        var compilation = GeneratorHarness.DomainCompilation();
        var driver = GeneratorHarness
            .CreateDriver(TestData.Valid())
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        var first = driver.GetRunResult().Results.Single();
        Reasons(first.TrackedSteps[LocalizationGenerator.TrackingNames.Input])
            .ShouldContain(IncrementalStepRunReason.New);

        var edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "namespace Clicalo.Domain.Edited; internal static class Added { }",
                GeneratorHarness.ParseOptions,
                "Added.cs",
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        var second = driver
            .RunGenerators(edited, TestContext.Current.CancellationToken)
            .GetRunResult()
            .Results.Single();

        var input = Reasons(second.TrackedSteps[LocalizationGenerator.TrackingNames.Input]);
        input.ShouldNotBeEmpty();
        input.ShouldAllBe(reason => IsReused(reason));
        var outputs = Reasons(second.TrackedOutputSteps[WellKnownGeneratorOutputs.SourceOutput]);
        outputs.ShouldNotBeEmpty();
        outputs.ShouldAllBe(reason => IsReused(reason));
    }

    private static bool IsReused(IncrementalStepRunReason reason) =>
        reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged;

    private static List<IncrementalStepRunReason> Reasons(
        IEnumerable<IncrementalGeneratorRunStep> steps
    ) => [.. steps.SelectMany(static step => step.Outputs).Select(static output => output.Reason)];
}
