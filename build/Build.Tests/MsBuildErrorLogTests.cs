namespace Clicalo.Build.Tests;

public sealed class MsBuildErrorLogTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "clicalo-root");

    [Fact]
    public void Reads_a_compiler_error_with_the_multi_process_prefix()
    {
        var file = Path.Combine(Root, "src", "Foo.cs");
        var project = Path.Combine(Root, "src", "Foo.csproj");

        var diagnostics = MsBuildErrorLog.Parse([
            $"  7:15>{file}(5,34): error CS0103: The name 'x' does not exist [{project}]",
        ]);

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Origin.ShouldBe(file);
        diagnostic.Line.ShouldBe(5);
        diagnostic.Column.ShouldBe(34);
        diagnostic.Code.ShouldBe("CS0103");
        diagnostic.Message.ShouldBe("The name 'x' does not exist");
        diagnostic.Project.ShouldBe(project);
    }

    [Fact]
    public void Reads_errors_without_a_location_or_a_code()
    {
        var diagnostics = MsBuildErrorLog.Parse([
            "CSC : fatal error CS2001: Source file 'x.cs' could not be found.",
            "MSBUILD : error : Something failed",
        ]);

        diagnostics.Count.ShouldBe(2);
        diagnostics[0].Origin.ShouldBe("CSC");
        diagnostics[0].Line.ShouldBeNull();
        diagnostics[0].Code.ShouldBe("CS2001");
        diagnostics[1].Code.ShouldBeNull();
        diagnostics[1].Message.ShouldBe("Something failed");
    }

    [Fact]
    public void Reads_restore_errors_reported_on_a_project_file()
    {
        var project = Path.Combine(Root, "build", "Build.csproj");

        var diagnostic = MsBuildErrorLog
            .Parse([
                $"{project} : error NU3034: Package 'Bullseye 6.2.0' from source 'https://api.nuget.org/v3/index.json': This package is signed but not by a trusted signer. [{Root}{Path.DirectorySeparatorChar}Clicalo.slnx]",
            ])
            .ShouldHaveSingleItem();

        diagnostic.Origin.ShouldBe(project);
        diagnostic.Code.ShouldBe("NU3034");
        diagnostic.Message.ShouldEndWith("not by a trusted signer.");
    }

    [Fact]
    public void Repeated_errors_of_several_target_frameworks_are_listed_once()
    {
        var line = $"{Path.Combine(Root, "a.cs")}(1,1): error CS1002: ; expected";

        MsBuildErrorLog.Parse([line, line, "", line]).Count.ShouldBe(1);
    }

    [Fact]
    public void Unrecognized_lines_continue_the_previous_error()
    {
        var diagnostic = MsBuildErrorLog
            .Parse([
                $"{Path.Combine(Root, "a.csproj")} : error NU1605: Detected package downgrade:",
                "  A 1.0 -> B (>= 2.0)",
            ])
            .ShouldHaveSingleItem();

        diagnostic.Message.ShouldBe("Detected package downgrade: A 1.0 -> B (>= 2.0)");
    }

    [Fact]
    public void Renders_repository_files_as_links_to_the_line()
    {
        var layout = RepoLayout.FromRoot(Root);
        var file = Path.Combine(Root, "src", "Foo.cs");
        MsBuildDiagnostic[] diagnostics =
        [
            new(file, 12, 5, "CS0103", "List<int> is *not* here", Path.Combine(Root, "Foo.csproj")),
        ];

        var markdown = MsBuildErrorLog.Render(
            diagnostics,
            layout,
            Path.Combine(Root, "artifacts", "cl")
        );

        markdown.ShouldBe(
            "1. [src/Foo.cs, línea 12, columna 5](<../../src/Foo.cs#L12>): CS0103 List\\<int\\> is \\*not\\* here (Foo)\n"
        );
    }

    [Fact]
    public void Long_lists_are_truncated_with_a_pointer_to_the_log()
    {
        var diagnostics = Enumerable
            .Range(1, MsBuildErrorLog.MaxListed + 5)
            .Select(line => new MsBuildDiagnostic("CSC", line, null, "CS0001", "x", null))
            .ToList();

        var markdown = MsBuildErrorLog.Render(
            diagnostics,
            RepoLayout.FromRoot(Root),
            Path.Combine(Root, "artifacts", "cl")
        );

        markdown.ShouldContain("Se muestran 50 de 55.");
    }
}
