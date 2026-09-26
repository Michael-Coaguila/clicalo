namespace Clicalo.Architecture.Tests.BannedApis;

/// <summary>
/// The BannedSymbols lists (blueprint §4.4, mechanism 3) are wired for product projects only, by layer, and every
/// listed overload, CsWin32 ones included, raises RS0030 in a real build; generated code does not.
/// </summary>
[Collection(SharedEnforcementBuild.Name)]
[Trait("Category", "Slow")]
public sealed class BannedSymbolsBuildTests(EnforcementBuild build)
{
    [Fact]
    [Trait("Req", "NFR-013")]
    public void The_All_and_Domain_lists_ban_their_apis_in_the_Domain_project() =>
        BannedLinesIn("banned-domain", "Probe.cs")
            .ShouldBe(BannedApiProbes.BannedLines(BannedApiProbes.Domain));

    [Fact]
    [Trait("Req", "NFR-013")]
    public void The_All_and_Application_lists_ban_their_apis_in_the_Application_project() =>
        BannedLinesIn("banned-application", "Probe.cs")
            .ShouldBe(BannedApiProbes.BannedLines(BannedApiProbes.Application));

    [Fact]
    [Trait("Req", "NFR-012")]
    public void The_All_and_Presentation_lists_ban_their_apis_but_not_ICommand_in_Presentation() =>
        BannedLinesIn("banned-presentation", "Probe.cs")
            .ShouldBe(BannedApiProbes.BannedLines(BannedApiProbes.Presentation));

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "SEG-007")]
    public void The_All_and_Surfaces_lists_ban_every_CsWin32_overload_and_WPF_popup_in_UI_Wpf() =>
        BannedLinesIn("banned-surfaces", "Probe.cs")
            .ShouldBe(BannedApiProbes.BannedLines(BannedApiProbes.Surfaces));

    [Fact]
    public void Generated_code_is_not_subject_to_the_lists() =>
        build
            .DiagnosticsOf("banned-surfaces")
            .Where(d => string.Equals(d.Code, "RS0030", StringComparison.Ordinal))
            .ShouldAllBe(d => Path.GetFileName(d.File) == "Probe.cs", build.Result.Output);

    [Fact]
    public void Banned_apis_are_errors() =>
        build
            .DiagnosticsOf("banned-domain")
            .Where(d => string.Equals(d.Code, "RS0030", StringComparison.Ordinal))
            .ShouldAllBe(d => d.Severity == "error");

    [Fact]
    public void The_lists_do_not_apply_outside_src()
    {
        build.DiagnosticsOf("not-product").ShouldBeEmpty(build.Result.Output);
        build.Compiled("not-product", "Clicalo.DevCli").ShouldBeTrue(build.Result.Output);
    }

    private int[] BannedLinesIn(string scenario, string file)
    {
        var lines = build
            .DiagnosticsOf(scenario)
            .Where(d =>
                string.Equals(d.Code, "RS0030", StringComparison.Ordinal)
                && string.Equals(Path.GetFileName(d.File), file, StringComparison.OrdinalIgnoreCase)
            )
            .Select(d => d.Line)
            .Distinct()
            .Order()
            .ToArray();
        lines.ShouldNotBeEmpty(build.Result.Output);
        return lines;
    }
}
