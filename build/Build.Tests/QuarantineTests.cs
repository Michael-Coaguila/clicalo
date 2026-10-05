using System.Text.RegularExpressions;

namespace Clicalo.Build.Tests;

/// <summary>
/// A quarantined test (<c>Category=Quarantine</c>) leaves the pull request tier and runs only every night
/// (<c>cl quarantine</c>), so it must say where its flakiness is tracked: the same file carries
/// <c>[Trait("Issue", "&lt;number&gt;")]</c> with the GitHub issue (label <c>flaky</c>). See
/// docs/architecture/testing-strategy.md, «Pruebas inestables».
/// </summary>
public sealed partial class QuarantineTests
{
    private static readonly string Root = RepoLayout.Locate(AppContext.BaseDirectory).Root;

    [Fact]
    public void Every_quarantined_test_names_its_GitHub_issue()
    {
        var unregistered = new[]
        {
            Path.Combine(Root, "tests"),
            Path.Combine(Root, "build", "Build.Tests"),
        }
            .SelectMany(static folder =>
                Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            )
            .Where(static file =>
                !file.Contains(
                    Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal
                )
            )
            .Select(static file => (File: file, Text: File.ReadAllText(file)))
            .Where(static source =>
                QuarantineTrait().IsMatch(source.Text) && !IssueTrait().IsMatch(source.Text)
            )
            .Select(source => Path.GetRelativePath(Root, source.File).Replace('\\', '/'));

        unregistered.ShouldBeEmpty(
            "Una prueba en cuarentena lleva también [Trait(\"Issue\", \"<número>\")] con su issue de GitHub."
        );
    }

    [Theory]
    [InlineData("[Trait(\"Category\", \"Quarantine\")]", true)]
    [InlineData("[Trait( \"Category\" , \"Quarantine\" )]", true)]
    [InlineData("[Trait(\"Category\", \"Perf\")]", false)]
    public void The_quarantine_trait_is_recognised(string source, bool quarantined) =>
        QuarantineTrait().IsMatch(source).ShouldBe(quarantined);

    [Theory]
    [InlineData("[Trait(\"Issue\", \"42\")]", true)]
    [InlineData("[Trait(\"Issue\", \"\")]", false)]
    [InlineData("[Trait(\"Issue\", \"TODO\")]", false)]
    public void Only_an_issue_number_registers_the_quarantine(string source, bool registered) =>
        IssueTrait().IsMatch(source).ShouldBe(registered);

    [GeneratedRegex(
        """\[\s*Trait\s*\(\s*"Category"\s*,\s*"Quarantine"\s*\)\s*\]""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex QuarantineTrait();

    [GeneratedRegex(
        """\[\s*Trait\s*\(\s*"Issue"\s*,\s*"[1-9][0-9]*"\s*\)\s*\]""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex IssueTrait();
}
