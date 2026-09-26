using System.Text.RegularExpressions;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests.BannedApis;

/// <summary>
/// Per-file exceptions to the banned-API lists (blueprint §4.4, mechanism 3): an RS0030 suppression is only valid in a
/// file registered in architecture/banned-api-exceptions.json, scoped (a justified [SuppressMessage], or a pragma that
/// is restored in the same file) and never global. The confined-API rules then check in the IL which API each allowed
/// file really reaches.
/// </summary>
public sealed partial class BannedApiExceptionsTests
{
    [Fact]
    public void Rs0030_is_suppressed_only_in_registered_files()
    {
        var globs = ArchitectureDocuments
            .BannedApiExceptions()
            .Exceptions.SelectMany(e => e.Paths)
            .Select(p => new Glob(p))
            .ToList();

        var findings = ProductSources()
            .SelectMany(file => SuppressionScanner.Scan(file.Relative, File.ReadAllText(file.Full)))
            .Where(finding =>
                finding.Kind != SuppressionKind.Scoped
                || !globs.Any(glob => glob.IsMatch(finding.File))
            )
            .Select(finding => finding.ToString())
            .ToList();

        findings.ShouldBeEmpty();
    }

    [Fact]
    public void No_build_or_editor_configuration_weakens_RS0030()
    {
        string[] patterns = ["*.csproj", "*.props", "*.targets", ".editorconfig", "*.globalconfig"];
        var offenders = patterns
            .SelectMany(pattern =>
                Directory.EnumerateFiles(RepoPaths.Root, pattern, SearchOption.AllDirectories)
            )
            .Select(path =>
                (path, relative: Path.GetRelativePath(RepoPaths.Root, path).Replace('\\', '/'))
            )
            .Where(file => !IsIgnored(file.relative))
            .Where(file => Rs0030Setting().IsMatch(File.ReadAllText(file.path)))
            .Select(file => file.relative)
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("dotnet_diagnostic.RS0030.severity = none", true)]
    [InlineData("dotnet_analyzer_diagnostic.category-ApiDesign.severity = silent", true)]
    [InlineData("dotnet_analyzer_diagnostic.severity = suggestion", true)]
    [InlineData("<NoWarn>$(NoWarn);RS0030</NoWarn>", true)]
    [InlineData("<WarningsNotAsErrors>RS0030</WarningsNotAsErrors>", true)]
    [InlineData("dotnet_analyzer_diagnostic.severity = warning", false)]
    [InlineData("<NoWarn>$(NoWarn);CS1591</NoWarn>", false)]
    public void The_configuration_check_recognises_each_way_to_weaken_RS0030(
        string setting,
        bool weakens
    ) => Rs0030Setting().IsMatch(setting).ShouldBe(weakens);

    [Theory]
    [InlineData(
        "[SuppressMessage(\"ApiDesign\", \"RS0030:Do not use banned APIs\", Justification = \"The adapter of IForegroundControl.\")]",
        SuppressionKind.Scoped
    )]
    [InlineData(
        "[SuppressMessage(\"ApiDesign\", \"RS0030:Do not use banned APIs\")]",
        SuppressionKind.Unjustified
    )]
    [InlineData(
        "[SuppressMessage(\"ApiDesign\", \"RS0030\", Justification = \"\")]",
        SuppressionKind.Unjustified
    )]
    [InlineData(
        "[assembly: SuppressMessage(\"ApiDesign\", \"RS0030\", Justification = \"Everything is allowed here.\")]",
        SuppressionKind.Global
    )]
    [InlineData(
        "#pragma warning disable RS0030 // The adapter of IForegroundControl.\nx();\n#pragma warning restore RS0030",
        SuppressionKind.Scoped
    )]
    [InlineData(
        "#pragma warning disable RS0030\nx();\n#pragma warning restore RS0030",
        SuppressionKind.Unjustified
    )]
    [InlineData(
        "#pragma warning disable RS0030 // The adapter of IForegroundControl.\nx();",
        SuppressionKind.Unrestored
    )]
    [InlineData(
        "#pragma warning disable CS1591, RS0030 // Justified in the file header.",
        SuppressionKind.Unrestored
    )]
    [InlineData(
        "#pragma warning disable // Every warning of this file, RS0030 included.\nx();\n#pragma warning restore",
        SuppressionKind.Blanket
    )]
    [InlineData(
        """
            [SuppressMessage(
                "ApiDesign",
                "RS0030:Do not use banned APIs",
                Justification = "The adapter of IForegroundControl."
            )]
            static void X() { }
            """,
        SuppressionKind.Scoped
    )]
    [InlineData(
        """
            [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(
                "ApiDesign",
                "RS0030"
            )]
            static void X() { }
            """,
        SuppressionKind.Unjustified
    )]
    [InlineData(
        """
            [module:
                SuppressMessage("ApiDesign", "RS0030", Justification = "Everything is allowed here.")]
            """,
        SuppressionKind.Global
    )]
    public void The_scanner_classifies_each_suppression(string source, SuppressionKind expected) =>
        SuppressionScanner.Scan("src/X.cs", source).ShouldHaveSingleItem().Kind.ShouldBe(expected);

    [Fact]
    public void The_scanner_reports_the_line_where_the_suppression_starts() =>
        SuppressionScanner
            .Scan(
                "src/X.cs",
                "namespace N;\n\nstatic class C\n{\n    [SuppressMessage(\n        \"ApiDesign\",\n        \"RS0030\"\n    )]\n    static void X() { }\n}\n"
            )
            .ShouldHaveSingleItem()
            .Line.ShouldBe(5);

    [Fact]
    public void The_scanner_ignores_other_diagnostics_comments_and_strings() =>
        SuppressionScanner
            .Scan(
                "src/X.cs",
                """
                #pragma warning disable CA1822 // RS0030 is not this one
                // SuppressMessage RS0030 in prose
                [SuppressMessage("Design", "CA1062", Justification = "Mentions RS0030 only here.")]
                static string Text() => "[SuppressMessage(\"ApiDesign\", \"RS0030\")]";
                """
            )
            .ShouldBeEmpty();

    [Fact]
    public void Registered_exceptions_are_confined_to_their_files()
    {
        var globs = ArchitectureDocuments
            .BannedApiExceptions()
            .Exceptions.SelectMany(e => e.Paths)
            .Select(p => new Glob(p))
            .ToList();

        globs.ShouldContain(glob =>
            glob.IsMatch("src/Clicalo.Platform.Windows/Foreground/ForegroundControl.cs")
        );
        globs.ShouldNotContain(glob =>
            glob.IsMatch("src/Clicalo.Platform.Windows/Foreground/ForegroundMonitor.cs")
        );
        globs.ShouldNotContain(glob =>
            glob.IsMatch("src/Clicalo.Presentation/Panel/PanelViewModel.cs")
        );
        globs.ShouldNotContain(glob => glob.IsMatch("src/Clicalo.Domain/Library/SecretText.cs"));
    }

    private static IEnumerable<(string Full, string Relative)> ProductSources() =>
        Directory
            .EnumerateFiles(RepoPaths.Combine("src"), "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, Path.GetRelativePath(RepoPaths.Root, path).Replace('\\', '/')))
            .Where(file =>
                !file.Item2.Contains("/obj/", StringComparison.Ordinal)
                && !file.Item2.Contains("/bin/", StringComparison.Ordinal)
            );

    private static bool IsIgnored(string relative) =>
        relative.StartsWith("artifacts/", StringComparison.Ordinal)
        || relative.StartsWith("docs/design/handoff/", StringComparison.Ordinal)
        || relative.StartsWith(".git/", StringComparison.Ordinal)
        || relative.Contains("/obj/", StringComparison.Ordinal)
        || relative.Contains("/bin/", StringComparison.Ordinal);

    [GeneratedRegex(
        @"RS0030\s*\.severity|dotnet_analyzer_diagnostic\.(?:category-ApiDesign\.)?severity\s*=\s*(?:none|silent|suggestion)|<(?:NoWarn|WarningsNotAsErrors)>[^<]*RS0030",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Rs0030Setting();
}
