using System.Xml.Linq;

namespace Clicalo.Build.Tests;

/// <summary>NFR-014: dependencies with fixed versions, checked by <c>cl check</c> (blueprint §2.1, §10.5).</summary>
[Trait("Req", "NFR-014")]
public sealed class VersionPinsTests
{
    [Theory]
    [InlineData("1.2.3", true)]
    [InlineData("10.0.12", true)]
    [InlineData("0.3.335", true)]
    [InlineData("2026.2.0", true)]
    [InlineData("1.2.3.4", true)]
    [InlineData("0.1.42-alpha", true)]
    [InlineData("71.0.14-preview.2+build.7", true)]
    [InlineData("[1.2.3]", true)]
    [InlineData("1.*", false)]
    [InlineData("1.2.*", false)]
    [InlineData("[1.0,2.0)", false)]
    [InlineData("(,2.0]", false)]
    [InlineData("[1.0,)", false)]
    [InlineData("$(SomeVersion)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_exact_versions_are_pins(string? version, bool exact) =>
        VersionPins.IsExactVersion(version).ShouldBe(exact);

    [Fact]
    public void Central_versions_must_be_exact()
    {
        var document = XDocument.Parse(
            """
            <Project>
              <ItemGroup>
                <GlobalPackageReference Include="Analyzer" Version="1.0.0" />
                <PackageVersion Include="Pinned" Version="2.0.0" />
                <PackageVersion Include="Floating" Version="2.*" />
                <PackageVersion Include="Ranged" Version="[1.0,2.0)" />
                <PackageVersion Include="Missing" />
              </ItemGroup>
            </Project>
            """,
            LoadOptions.SetLineInfo
        );

        var violations = VersionPins
            .CheckCentralPackages("Directory.Packages.props", document)
            .ToList();

        violations
            .Select(violation => violation.Subject)
            .ShouldBe(["Floating 2.*", "Ranged [1.0,2.0)", "Missing"]);
        violations[0].Line.ShouldBe(5);
    }

    [Fact]
    public void Projects_never_carry_versions()
    {
        var document = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Central" />
                <PackageReference Include="Inline" Version="1.0.0" />
                <PackageReference Include="Override" VersionOverride="1.0.0" />
                <PackageReference Include="Element"><Version>1.0.0</Version></PackageReference>
                <PackageVersion Include="Stray" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """
        );

        VersionPins
            .CheckProjectFile("src/App/App.csproj", document)
            .Select(violation => violation.Subject)
            .ShouldBe(["Inline", "Override", "Element", "Stray"]);
    }

    [Fact]
    public void Actions_are_pinned_by_full_sha_with_a_version_comment()
    {
        const string Workflow = """
            jobs:
              verify:
                steps:
                  - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
                  - uses: actions/setup-dotnet@v6
                  - name: Cache
                    uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9
                  - uses: "github/codeql-action/init@2892aa5e19bbd11bc0cff5427e3b750a04d9e3c2" # v4.38.2
                  - uses: ./.github/actions/local
                  - uses: docker://alpine:3.20
                  - uses: actions/upload-artifact@043fb46d # v7.0.1
            """;

        var violations = VersionPins.CheckWorkflow(".github/workflows/pr.yml", Workflow);

        violations
            .Select(violation => (violation.Subject, violation.Line))
            .ShouldBe([
                ("actions/setup-dotnet@v6", 5),
                ("actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9", 7),
                ("actions/upload-artifact@043fb46d", 11),
            ]);
        violations[1].Problem.ShouldBe(Messages.PinActionNoVersionComment);
    }

    [Fact]
    public void Local_tools_have_exact_versions()
    {
        const string Manifest = """
            { "version": 1, "isRoot": true, "tools": {
                "csharpier": { "version": "1.3.0", "commands": ["csharpier"] },
                "floating": { "version": "1.*", "commands": ["floating"] },
                "missing": { "commands": ["missing"] } } }
            """;

        VersionPins
            .CheckToolManifest(".config/dotnet-tools.json", Manifest)
            .Select(violation => violation.Subject)
            .ShouldBe(["floating 1.*", "missing"]);
    }

    [Theory]
    [InlineData("""{ "sdk": { "version": "10.0.401", "rollForward": "latestPatch" } }""", 0)]
    [InlineData("""{ "sdk": { "version": "10.0.*" } }""", 1)]
    [InlineData("""{ "sdk": { "rollForward": "latestMajor" } }""", 1)]
    public void The_sdk_is_pinned(string json, int violations) =>
        VersionPins.CheckGlobalJson("global.json", json).Count.ShouldBe(violations);

    [Fact]
    public void This_repository_is_fully_pinned()
    {
        var layout = RepoLayout.Locate(AppContext.BaseDirectory);

        VersionPins.Check(layout).ShouldBeEmpty();
    }
}
