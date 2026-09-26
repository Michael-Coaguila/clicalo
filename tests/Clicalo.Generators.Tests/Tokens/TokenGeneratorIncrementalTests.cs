using Clicalo.Generators.Tokens;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Clicalo.Generators.Tests.Tokens;

/// <summary>
/// The generator only reads data/tokens and the generator profile: editing C# must not run it again (blueprint §8.4,
/// incremental generators). A pipeline that starts reading the compilation fails here.
/// </summary>
public sealed class TokenGeneratorIncrementalTests
{
    [Fact]
    public void A_code_edit_leaves_the_input_and_the_generated_sources_cached()
    {
        var compilation = TokenGeneratorHarness.UiCompilation();
        var driver = TokenGeneratorHarness
            .CreateDriver(TokenTestData.Files)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        var first = driver.GetRunResult().Results.Single();
        Reasons(first.TrackedSteps[TokenGenerator.TrackingNames.Input])
            .ShouldContain(IncrementalStepRunReason.New);

        var edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "namespace Clicalo.UI.Wpf.Edited; internal static class Added { }",
                TokenGeneratorHarness.ParseOptions,
                "Added.cs",
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        var second = driver
            .RunGenerators(edited, TestContext.Current.CancellationToken)
            .GetRunResult()
            .Results.Single();

        var input = Reasons(second.TrackedSteps[TokenGenerator.TrackingNames.Input]);
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
