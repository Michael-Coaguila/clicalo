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
    public void The_scanner_classifies_each_suppression(string source, SuppressionKind expected) =>
        SuppressionScanner.Scan("src/X.cs", source).ShouldHaveSingleItem().Kind.ShouldBe(expected);

    [Fact]
    public void The_scanner_ignores_other_diagnostics_and_comments() =>
        SuppressionScanner
            .Scan(
                "src/X.cs",
                "#pragma warning disable CA1822 // RS0030 is not this one\n// SuppressMessage RS0030 in prose\n"
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
        @"RS0030\s*\.severity|<(?:NoWarn|WarningsNotAsErrors)>[^<]*RS0030",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Rs0030Setting();
}

/// <summary>How an RS0030 suppression is written.</summary>
public enum SuppressionKind
{
    /// <summary>A justified [SuppressMessage], or a justified pragma restored in the same file.</summary>
    Scoped,

    /// <summary>No justification.</summary>
    Unjustified,

    /// <summary>A pragma that is never restored, so it covers the rest of the file.</summary>
    Unrestored,

    /// <summary>An assembly-level suppression.</summary>
    Global,
}

/// <summary>Finds the RS0030 suppressions of a C# source file.</summary>
internal static partial class SuppressionScanner
{
    public static IEnumerable<SuppressionFinding> Scan(string file, string source)
    {
        var lines = source.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var code = line.Split("//", 2)[0];
            if (
                Pragma().Match(line) is { Success: true } pragma
                && string.Equals(pragma.Groups["action"].Value, "disable", StringComparison.Ordinal)
                && Rs0030().IsMatch(code)
            )
            {
                var justified =
                    line.Contains("//", StringComparison.Ordinal)
                    && line.Split("//", 2)[1].Trim().Length >= 10;
                var restored = lines
                    .Skip(i + 1)
                    .Any(l =>
                        Pragma().Match(l) is { Success: true } p
                        && string.Equals(
                            p.Groups["action"].Value,
                            "restore",
                            StringComparison.Ordinal
                        )
                        && Rs0030().IsMatch(l.Split("//", 2)[0])
                    );
                var kind =
                    !justified ? SuppressionKind.Unjustified
                    : restored ? SuppressionKind.Scoped
                    : SuppressionKind.Unrestored;
                yield return new SuppressionFinding(file, i + 1, kind);
            }
            else if (
                code.Contains("SuppressMessage", StringComparison.Ordinal) && Rs0030().IsMatch(code)
            )
            {
                var kind =
                    AssemblyTarget().IsMatch(code) ? SuppressionKind.Global
                    : Justification().IsMatch(code) ? SuppressionKind.Scoped
                    : SuppressionKind.Unjustified;
                yield return new SuppressionFinding(file, i + 1, kind);
            }
        }
    }

    [GeneratedRegex(
        @"^\s*#\s*pragma\s+warning\s+(?<action>disable|restore)\b",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Pragma();

    [GeneratedRegex(@"\bRS0030\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Rs0030();

    [GeneratedRegex(
        @"\[\s*(?:assembly|module)\s*:",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex AssemblyTarget();

    [GeneratedRegex(
        @"Justification\s*=\s*""[^""]{10,}""",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Justification();
}

/// <summary>An RS0030 suppression found in a source file.</summary>
internal sealed record SuppressionFinding(string File, int Line, SuppressionKind Kind)
{
    public override string ToString() => File + "(" + Line + "): " + Kind + " RS0030 suppression";
}
