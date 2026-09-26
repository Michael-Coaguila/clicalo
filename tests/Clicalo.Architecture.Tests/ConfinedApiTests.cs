using ArchUnitNET.xUnitV3;
using Clicalo.Architecture.Tests.Support;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// The banned-API table (blueprint §4.4, mechanism 3) checked in the compiled product: each confined API is reached
/// only from where the table allows it, whatever the overload, suppression or generated code involved.
/// </summary>
public sealed class ConfinedApiTests
{
    private static readonly Scope Fixture = Scope.Fixture("ConfinedApis");

    public static TheoryData<string> Apis =>
        [.. ConfinedApis.Catalog(Scope.Product).Select(api => api.Name)];

    [Theory]
    [MemberData(nameof(Apis))]
    [Trait("Req", "REG-01")]
    [Trait("Req", "SEG-007")]
    [Trait("Req", "NFR-009")]
    [Trait("Req", "NFR-013")]
    public void Confined_apis_are_used_only_where_the_table_allows(string api) =>
        ConfinedApis
            .Catalog(Scope.Product)
            .Single(entry => string.Equals(entry.Name, api, StringComparison.Ordinal))
            .Rule(Scope.Product.Universe)
            .Check(Product.Architecture);

    [Theory]
    [InlineData("SetForegroundWindow", "PInvoke::SetForegroundWindow(System.IntPtr)")]
    [InlineData("AttachThreadInput", "PInvoke::AttachThreadInput(")]
    [InlineData("SendInput", "PInvoke::SendInput(System.UInt32)")]
    [InlineData("process exit", "System.Environment::Exit(System.Int32)")]
    [InlineData("clock, timers, ids and randomness", "System.DateTime::get_Now()")]
    [InlineData(
        "clock, timers, ids and randomness",
        "System.Threading.Thread::Sleep(System.TimeSpan)"
    )]
    [InlineData(
        "clock, timers, ids and randomness",
        "System.Threading.Tasks.Task::Delay(System.TimeSpan)"
    )]
    [InlineData("blocking waits", "get_Result()")]
    [InlineData("Console", "uses System.Console")]
    [InlineData("file writes", "System.IO.File::WriteAllText(System.String,System.String)")]
    [InlineData("file writes", "System.IO.FileStream::.ctor(System.String,System.IO.FileMode)")]
    [InlineData("Process.Start", "System.Diagnostics.Process::Start(System.String)")]
    public void Using_a_confined_api_outside_its_zone_fails_the_rule(string api, string expected)
    {
        var rule = FixtureRule(api);

        rule.HasNoViolations(FixtureArchitecture.Architecture).ShouldBeFalse();
        var violations = Rule.Violations(rule, FixtureArchitecture.Architecture);
        violations.ShouldContain(
            FixtureArchitecture.Name("ConfinedApis.Presentation.Rogue.Offender"),
            Case.Sensitive
        );
        violations.ShouldContain(expected, Case.Sensitive);
    }

    [Theory]
    [InlineData("SetForegroundWindow", "Platform.Windows.Foreground.ForegroundControl")]
    [InlineData("process exit", "App.Lifecycle.AppLifetime")]
    [InlineData("clock, timers, ids and randomness", "Infrastructure.Clock.SystemSources")]
    public void Using_a_confined_api_inside_its_zone_passes_the_rule(
        string api,
        string allowedType
    ) =>
        Rule.Violations(FixtureRule(api), FixtureArchitecture.Architecture)
            .ShouldNotContain(
                FixtureArchitecture.Name("ConfinedApis." + allowedType),
                Case.Sensitive
            );

    private static ArchUnitNET.Fluent.IArchRule FixtureRule(string api) =>
        ConfinedApis
            .Catalog(Fixture)
            .Single(entry => string.Equals(entry.Name, api, StringComparison.Ordinal))
            .Rule(Fixture.Universe.Except(Fixture.Namespace("Windows.Win32")));
}
