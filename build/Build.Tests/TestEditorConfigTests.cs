namespace Clicalo.Build.Tests;

/// <summary>
/// build/Build.Tests is a test project outside tests/, so it carries its own copy of the test relaxations of
/// tests/.editorconfig (docs/architecture/tooling.md, "Supresiones"). The copy may not relax anything else.
/// </summary>
public sealed class TestEditorConfigTests
{
    [Fact]
    public void Build_Tests_relaxes_exactly_what_the_tests_folder_relaxes()
    {
        var root = RepoLayout.Locate(AppContext.BaseDirectory).Root;

        Rules(Path.Combine(root, "build", "Build.Tests", ".editorconfig"))
            .ShouldBe(Rules(Path.Combine(root, "tests", ".editorconfig")));
    }

    /// <summary>Every line that is not blank or a comment: sections and settings, in order.</summary>
    private static List<string> Rules(string path) =>
        [
            .. File.ReadAllLines(path)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0 && !line.StartsWith('#')),
        ];
}
