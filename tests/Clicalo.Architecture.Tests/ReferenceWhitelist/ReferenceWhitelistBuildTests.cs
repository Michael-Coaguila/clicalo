namespace Clicalo.Architecture.Tests.ReferenceWhitelist;

/// <summary>
/// The ClicaloVerifyReferences target (blueprint §4.4, mechanism 1) fails a real build, before compiling, when a
/// project declares a reference that architecture/allowed-dependencies.json does not allow.
/// </summary>
[Collection(SharedEnforcementBuild.Name)]
[Trait("Category", "Slow")]
public sealed class ReferenceWhitelistBuildTests(EnforcementBuild build)
{
    [Fact]
    [Trait("Req", "NFR-014")]
    public void A_forbidden_project_reference_fails_the_build_with_CLCA001_before_compiling()
    {
        var error = SingleError("forbidden-project", "CLCA001");

        error.Message.ShouldStartWith(
            "Project reference 'Clicalo.Infrastructure' is not allowed in 'Clicalo.Domain'. architecture/allowed-dependencies.json allows project references: (none). Rule for Clicalo.Domain: Pure domain",
            Case.Sensitive
        );
        error.File.ShouldEndWith(
            Path.Combine("Clicalo.Domain", "Clicalo.Domain.csproj"),
            Case.Insensitive
        );
        error.Line.ShouldBe(7);
        build.Compiled("forbidden-project", "Clicalo.Domain").ShouldBeFalse();
        build.Result.ExitCode.ShouldNotBe(0);
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void A_forbidden_package_reference_fails_the_build_with_CLCA001_before_compiling()
    {
        var error = SingleError("forbidden-package", "CLCA001");

        error.Message.ShouldStartWith(
            "Package reference 'Serilog' is not allowed in 'Clicalo.Domain'. architecture/allowed-dependencies.json allows package references: (none).",
            Case.Sensitive
        );
        error.Line.ShouldBe(7);
        build.Compiled("forbidden-package", "Clicalo.Domain").ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "NFR-012")]
    public void WPF_in_Presentation_fails_the_build_with_CLCA001_and_the_Windows_TFM_with_CLCA003()
    {
        SingleError("forbidden-framework", "CLCA001")
            .Message.ShouldStartWith(
                "Framework reference 'Microsoft.WindowsDesktop.App.WPF' (UseWPF=true) is not allowed in 'Clicalo.Presentation'.",
                Case.Sensitive
            );
        SingleError("forbidden-framework", "CLCA003")
            .Message.ShouldStartWith(
                "Project 'Clicalo.Presentation' must target a portable framework (no OS platform) but targets the 'windows' platform.",
                Case.Sensitive
            );
        build.Compiled("forbidden-framework", "Clicalo.Presentation").ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void An_analyzer_reference_to_a_project_that_is_not_an_analyzer_fails_the_build()
    {
        SingleError("analyzer-misuse", "CLCA001")
            .Message.ShouldStartWith(
                "Analyzer project reference 'Clicalo.Infrastructure' is not allowed in 'Clicalo.Domain'.",
                Case.Sensitive
            );
        build.Compiled("analyzer-misuse", "Clicalo.Domain").ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void A_project_missing_from_the_whitelist_fails_the_build_with_CLCA002()
    {
        SingleError("undeclared", "CLCA002")
            .Message.ShouldStartWith(
                "Project 'Clicalo.Rogue' is not declared in architecture/allowed-dependencies.json.",
                Case.Sensitive
            );
        build.Compiled("undeclared", "Clicalo.Rogue").ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void Allowed_references_build_cleanly()
    {
        build.DiagnosticsOf("allowed").ShouldBeEmpty(build.Result.Output);
        build.Compiled("allowed", "Clicalo.Application").ShouldBeTrue(build.Result.Output);
        build.Compiled("allowed", "Clicalo.Domain").ShouldBeTrue(build.Result.Output);
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void The_temporary_project_of_WPF_markup_compilation_is_checked_as_its_real_project()
    {
        build.DiagnosticsOf("wpf-markup").ShouldBeEmpty(build.Result.Output);
        build.Compiled("wpf-markup", "Clicalo.UI.Wpf").ShouldBeTrue(build.Result.Output);
    }

    private Support.Diagnostic SingleError(string scenario, string code)
    {
        var diagnostics = build.DiagnosticsOf(scenario);
        var errors = diagnostics
            .Where(d => string.Equals(d.Code, code, StringComparison.Ordinal))
            .ToList();
        errors.Count.ShouldBe(1, build.Result.Output);
        errors[0].Severity.ShouldBe("error");
        return errors[0];
    }
}
