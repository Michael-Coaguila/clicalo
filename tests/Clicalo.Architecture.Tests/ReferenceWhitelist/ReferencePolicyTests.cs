using Clicalo.Architecture.Tasks;
using Clicalo.Architecture.Tests.Support;
using static Clicalo.Architecture.Tasks.ClicaloVerifyReferences;

namespace Clicalo.Architecture.Tests.ReferenceWhitelist;

/// <summary>
/// Unit tests of the policy behind the ClicaloVerifyReferences MSBuild task (architecture/tasks), compiled here
/// without its MSBuild adapter. The end-to-end behaviour is proved by <c>ReferenceWhitelistBuildTests</c>.
/// </summary>
public sealed class ReferencePolicyTests
{
    private const string PolicyJson = """
        {
          "$schema": "./schemas/allowed-dependencies.schema.json",
          "description": "Test policy.",
          "analyzerProjects": { "description": "Analyzers.", "projects": ["Clicalo.Generators"] },
          "globalPackages": { "description": "Global.", "packages": ["Meziantou.Analyzer"] },
          "projects": {
            "Clicalo.Domain": {
              "area": "src",
              "rule": "Pure domain.",
              "platform": "portable",
              "projectReferences": [],
              "packageReferences": []
            },
            "Clicalo.UI.Wpf": {
              "area": "src",
              "rule": "The only WPF-aware layer.",
              "platform": "windows",
              "projectReferences": ["Clicalo.Domain"],
              "packageReferences": ["Microsoft.Windows.CsWin32"],
              "frameworkReferences": ["Microsoft.WindowsDesktop.App.WPF"]
            }
          }
        }
        """;

    private static readonly Policy TestPolicy = Policy.Parse(PolicyJson);

    [Fact]
    [Trait("Req", "NFR-014")]
    public void The_repository_policy_parses_and_declares_every_product_project()
    {
        var policy = Policy.Parse(
            File.ReadAllText(ArchitectureDocuments.PathOf("allowed-dependencies.json"))
        );

        Product.AssemblyOfProject.Keys.ShouldAllBe(project => policy.Projects.ContainsKey(project));
        policy.Projects["Clicalo.Domain"].ProjectReferences.ShouldBeEmpty();
        policy.AnalyzerProjects.Contains("Clicalo.Analyzers").ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void Allowed_references_produce_no_finding()
    {
        var findings = Evaluate(
            TestPolicy,
            Facts("Clicalo.UI.Wpf", "windows", useWpf: true),
            [
                new DeclaredReference(ReferenceKind.Project, "Clicalo.Domain", false, "UI.csproj"),
                new DeclaredReference(
                    ReferenceKind.Project,
                    "Clicalo.Generators",
                    true,
                    "UI.csproj"
                ),
                new DeclaredReference(
                    ReferenceKind.Package,
                    "Microsoft.Windows.CsWin32",
                    false,
                    "UI.csproj"
                ),
                new DeclaredReference(
                    ReferenceKind.GlobalPackage,
                    "Meziantou.Analyzer",
                    false,
                    "Directory.Packages.props"
                ),
            ]
        );

        findings.ShouldBeEmpty();
    }

    [Theory]
    [Trait("Req", "NFR-014")]
    [InlineData("Project", "Clicalo.Infrastructure", "Project reference 'Clicalo.Infrastructure'")]
    [InlineData("Package", "Serilog", "Package reference 'Serilog'")]
    [InlineData("Framework", "Microsoft.WindowsDesktop.App.WPF", "Framework reference")]
    [InlineData("Assembly", "Legacy", "Prefer a PackageReference or a ProjectReference.")]
    [InlineData(
        "GlobalPackage",
        "Roslynator.Analyzers",
        "Global package reference 'Roslynator.Analyzers'"
    )]
    public void A_forbidden_reference_is_reported_as_CLCA001(
        string kind,
        string name,
        string expected
    )
    {
        var findings = Evaluate(
            TestPolicy,
            Facts("Clicalo.Domain", ""),
            [new DeclaredReference(ParseKind(kind), name, false, "")]
        );

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(ForbiddenReferenceCode);
        finding.Message.ShouldContain(expected, Case.Sensitive);
        finding.Message.ShouldContain("is not allowed in 'Clicalo.Domain'", Case.Sensitive);
        finding.Message.ShouldContain("Rule for Clicalo.Domain: Pure domain.", Case.Sensitive);
        finding.Message.ShouldContain("docs/architecture/enforcement.md", Case.Sensitive);
    }

    [Fact]
    public void The_message_lists_what_the_whitelist_allows()
    {
        var finding = Evaluate(
                TestPolicy,
                Facts("Clicalo.UI.Wpf", "windows"),
                [new DeclaredReference(ReferenceKind.Project, "Clicalo.Infrastructure", false, "")]
            )
            .ShouldHaveSingleItem();

        finding.Message.ShouldContain("allows project references: Clicalo.Domain.", Case.Sensitive);
    }

    [Fact]
    public void UseWPF_counts_as_the_WPF_framework_reference()
    {
        var finding = Evaluate(TestPolicy, Facts("Clicalo.Domain", "", useWpf: true), [])
            .ShouldHaveSingleItem();

        finding.Code.ShouldBe(ForbiddenReferenceCode);
        finding.Message.ShouldStartWith(
            "Framework reference 'Microsoft.WindowsDesktop.App.WPF' (UseWPF=true)",
            Case.Sensitive
        );
    }

    [Fact]
    public void An_analyzer_only_reference_is_allowed_only_to_an_analyzer_project()
    {
        var findings = Evaluate(
            TestPolicy,
            Facts("Clicalo.Domain", ""),
            [
                new DeclaredReference(ReferenceKind.Project, "Clicalo.Generators", true, ""),
                new DeclaredReference(ReferenceKind.Project, "Clicalo.Generators", false, ""),
                new DeclaredReference(ReferenceKind.Project, "Clicalo.Infrastructure", true, ""),
            ]
        );

        findings
            .Select(f => f.Message[..f.Message.IndexOf(" is not", StringComparison.Ordinal)])
            .ShouldBe(
                [
                    "Project reference 'Clicalo.Generators'",
                    "Analyzer project reference 'Clicalo.Infrastructure'",
                ],
                ignoreOrder: true
            );
    }

    [Fact]
    public void A_reference_declared_twice_is_reported_once()
    {
        var reference = new DeclaredReference(ReferenceKind.Package, "Serilog", false, "");

        Evaluate(TestPolicy, Facts("Clicalo.Domain", ""), [reference, reference]).Count.ShouldBe(1);
    }

    [Fact]
    public void An_undeclared_project_is_reported_as_CLCA002()
    {
        var finding = Evaluate(TestPolicy, Facts("Clicalo.Rogue", ""), []).ShouldHaveSingleItem();

        finding.Code.ShouldBe(UndeclaredProjectCode);
        finding.Message.ShouldContain(
            "'Clicalo.Rogue' is not declared in architecture/allowed-dependencies.json",
            Case.Sensitive
        );
    }

    [Theory]
    [InlineData(
        "Clicalo.Domain",
        "windows",
        "must target a portable framework (no OS platform) but targets the 'windows' platform"
    )]
    [InlineData("Clicalo.UI.Wpf", "", "must target Windows but targets a portable framework")]
    public void A_project_on_the_wrong_platform_is_reported_as_CLCA003(
        string project,
        string platform,
        string expected
    )
    {
        var finding = Evaluate(TestPolicy, Facts(project, platform), []).ShouldHaveSingleItem();

        finding.Code.ShouldBe(WrongPlatformCode);
        finding.Message.ShouldContain(expected, Case.Sensitive);
    }

    [Theory]
    [InlineData("{ \"a\": 1, \"a\": 2 }", 1, 11, "Duplicate property 'a'")]
    [InlineData("{\n  \"analyzerProjects\": [1, 2", 2, 28, "Expected ',' or ']'")]
    [InlineData("[]", 1, 1, "The document must be an object but is an array")]
    [InlineData("{}", 1, 1, "Missing required property 'analyzerProjects'")]
    [InlineData("{ \"s\": \"\\q\" }", 1, 9, "Invalid escape sequence")]
    public void An_invalid_policy_is_rejected_with_its_position(
        string json,
        int line,
        int column,
        string expected
    )
    {
        var ex = Should.Throw<PolicyFormatException>(() => Policy.Parse(json));

        ex.Line.ShouldBe(line);
        ex.Column.ShouldBe(column);
        ex.Message.ShouldStartWith(expected, Case.Sensitive);
    }

    [Theory]
    [InlineData(
        "\"platform\": \"portable\"",
        "\"platform\": \"linux\"",
        "platform must be \"portable\" or \"windows\""
    )]
    [InlineData(
        "\"projectReferences\": [\"Clicalo.Domain\"]",
        "\"projectReferences\": [\"Clicalo.Domain\", \"clicalo.domain\"]",
        "lists 'clicalo.domain' twice"
    )]
    [InlineData("\"rule\": \"Pure domain.\"", "\"rule\": \"  \"", "rule must explain the rule")]
    [InlineData(
        "\"packageReferences\": []",
        "\"packageReferences\": {}",
        "packageReferences must be an array but is an object"
    )]
    public void A_policy_with_an_invalid_value_is_rejected(
        string original,
        string replacement,
        string expected
    )
    {
        var json = ReplaceFirst(PolicyJson, original, replacement);

        Should
            .Throw<PolicyFormatException>(() => Policy.Parse(json))
            .Message.ShouldContain(expected, Case.Sensitive);
    }

    [Fact]
    public void Strings_decode_escapes_and_unicode()
    {
        var root = JsonReader.Parse("{ \"k\": \"a\\u00f1\\n\\\"b\\\" \\\\ \\/\" }");

        root.Required("k").Text.ShouldBe("añ\n\"b\" \\ /");
    }

    [Fact]
    public void Numbers_booleans_and_null_are_accepted()
    {
        var root = JsonReader.Parse("[ -1.5e+3, 0, true, false, null ]");

        root.Items.Select(i => i.Kind)
            .ShouldBe([
                JsonKind.Number,
                JsonKind.Number,
                JsonKind.Boolean,
                JsonKind.Boolean,
                JsonKind.Null,
            ]);
        root.Items[0].Text.ShouldBe("-1.5e+3");
    }

    [Theory]
    [InlineData("01")]
    [InlineData("1.")]
    [InlineData("tru")]
    [InlineData("\"open")]
    [InlineData("{ \"a\" 1 }")]
    [InlineData("[1] 2")]
    public void Malformed_json_is_rejected(string json) =>
        Should.Throw<PolicyFormatException>(() => JsonReader.Parse(json));

    [Theory]
    [InlineData("Foo, Version=1.0.0.0, Culture=neutral", "Foo")]
    [InlineData("lib\\net10.0\\Foo.Bar.dll", "Foo.Bar")]
    [InlineData("System.Xml", "System.Xml")]
    public void Assembly_references_are_named_by_their_simple_name(
        string itemSpec,
        string expected
    ) => ClicaloVerifyReferences.AssemblyName(itemSpec).ShouldBe(expected);

    [Theory]
    [InlineData("Analyzer", "false", true)]
    [InlineData("analyzer", "False", true)]
    [InlineData("Analyzer", "", false)]
    [InlineData("", "false", false)]
    public void Analyzer_only_references_need_both_metadata(
        string outputItemType,
        string referenceOutputAssembly,
        bool expected
    ) => IsAnalyzerOnly(outputItemType, referenceOutputAssembly).ShouldBe(expected);

    [Fact]
    public void Findings_point_at_the_line_that_declares_the_reference()
    {
        var file = Path.Combine(
            Path.GetTempPath(),
            "clicalo-locate-" + Environment.ProcessId + ".csproj"
        );
        File.WriteAllLines(
            file,
            [
                "<Project Sdk=\"Microsoft.NET.Sdk\">",
                "  <ItemGroup>",
                "    <PackageReference Include=\"Serilog.Extensions.Logging\" />",
                "    <PackageReference Include=\"Serilog\" />",
                "    <ProjectReference Include=\"..\\Clicalo.Infrastructure\\Clicalo.Infrastructure.csproj\" />",
                "  </ItemGroup>",
                "</Project>",
            ]
        );
        try
        {
            Locate(file, "Serilog").ShouldBe((4, 31));
            Locate(file, "Clicalo.Infrastructure").ShouldBe((5, 57));
            Locate(file, "Missing").ShouldBe((0, 0));
            Locate("", "Serilog").ShouldBe((0, 0));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Paths_are_shown_relative_to_the_repository()
    {
        var root = Path.Combine(Path.GetTempPath(), "repo");

        DisplayPath(root, Path.Combine(root, "architecture", "allowed-dependencies.json"))
            .ShouldBe("architecture/allowed-dependencies.json");
        DisplayPath(
                root + Path.DirectorySeparatorChar,
                Path.Combine(Path.GetTempPath(), "other", "x.json")
            )
            .ShouldBe(Path.Combine(Path.GetTempPath(), "other", "x.json"));
    }

    [Theory]
    [InlineData("Project")]
    [InlineData("GlobalPackage")]
    [InlineData("Assembly")]
    public void Kinds_map_from_the_ClicaloKind_metadata(string metadata) =>
        ParseKind(metadata).ToString().ShouldBe(metadata);

    [Fact]
    public void An_unknown_kind_is_a_defect_of_the_targets_file() =>
        Should.Throw<ArgumentException>(() => ParseKind("Other"));

    private static ProjectFacts Facts(string name, string platform, bool useWpf = false) =>
        new(
            name,
            name + ".csproj",
            platform,
            useWpf,
            false,
            "architecture/allowed-dependencies.json"
        );

    private static string ReplaceFirst(string text, string original, string replacement)
    {
        var index = text.IndexOf(original, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0);
        return string.Concat(
            text.AsSpan(0, index),
            replacement,
            text.AsSpan(index + original.Length)
        );
    }
}
