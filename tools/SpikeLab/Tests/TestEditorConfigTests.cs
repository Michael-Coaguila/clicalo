using Clicalo.TestKit;

namespace Clicalo.Tools.SpikeLab.Tests;

/// <summary>
/// tools/SpikeLab/Tests is a test project outside tests/, so it carries its own copy of the test relaxations of
/// tests/.editorconfig, like build/Build.Tests. The copy may not relax anything else.
/// </summary>
public sealed class TestEditorConfigTests
{
    [Fact]
    public void SpikeLab_Tests_relaxes_exactly_what_the_tests_folder_relaxes() =>
        Rules(RepoPaths.Combine("tools", "SpikeLab", "Tests", ".editorconfig"))
            .ShouldBe(Rules(RepoPaths.Combine("tests", ".editorconfig")));

    /// <summary>Every line that is not blank or a comment: sections and settings, in order.</summary>
    private static List<string> Rules(string path) =>
        [
            .. File.ReadAllLines(path)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0 && !line.StartsWith('#')),
        ];
}
