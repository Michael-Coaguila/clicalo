using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Clicalo.DevCli.Trace;

/// <summary>
/// <c>trace</c>: writes <c>artifacts/cl/trace.md</c>, the list of every requirement of the catalog with the tests
/// that name it in a requirement trait, and marks the MUST requirements without a test (blueprint §10.6). A MUST
/// without a test that <c>docs/guides/aceptacion-manual.md</c> names is «only in the manual script». It fails only
/// when a trait names an identifier that is not in the catalog. Output ends with one line Narrator can read aloud.
/// </summary>
internal static partial class TraceCommand
{
    /// <summary>The catalog, relative to the repository root.</summary>
    public const string CatalogPath = "docs/requirements/catalog.md";

    /// <summary>The manual acceptance script, relative to the repository root.</summary>
    public const string ManualScriptPath = "docs/guides/aceptacion-manual.md";

    /// <summary>The report, relative to the repository root.</summary>
    public const string ReportPath = "artifacts/cl/trace.md";

    /// <summary>Id of the diagnostic printed for each trait that names no requirement.</summary>
    public const string UnknownTraitId = "CLCT010";

    /// <summary>The folders whose C# files may hold tests.</summary>
    private static readonly string[] TestRoots = ["tests", "build", "tools", "generators"];

    private static readonly string[] SkippedFolders = ["bin", "obj", "artifacts"];

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static int Run(string root, TextWriter output)
    {
        IReadOnlyList<CatalogEntry> entries;
        try
        {
            entries = CatalogReader.Read(File.ReadAllText(Path.Combine(root, CatalogPath)));
        }
        catch (IOException ex)
        {
            output.WriteLine("error: " + ex.Message);
            output.WriteLine("trace: the catalog could not be read.");
            return ExitCodes.Failure;
        }

        if (entries.Count == 0)
        {
            output.WriteLine("trace: no requirement was found in " + CatalogPath + ".");
            return ExitCodes.Failure;
        }

        var (markdown, summary, unknown) = Analyze(root, entries);
        var report = Path.Combine(root, ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, markdown, Utf8NoBom);

        foreach (var reference in unknown)
        {
            output.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{reference.File}({reference.Line}): error {UnknownTraitId}: '{reference.Id}' is not a requirement of {CatalogPath}."
                )
            );
        }

        output.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"trace: {summary.Requirements} requirements; {summary.MustTested} of {summary.Must} MUST with a test, {summary.MustManualOnly} only in the manual script, {summary.MustUncovered} without any; {summary.UnknownTraits} unknown traits; report in {ReportPath}."
            )
        );
        return summary.UnknownTraits == 0 ? ExitCodes.Success : ExitCodes.Failure;
    }

    /// <summary>
    /// Reads the test sources and the manual script of <paramref name="root"/> and renders the report for
    /// <paramref name="entries"/>; it writes nothing.
    /// </summary>
    public static (
        string Markdown,
        TraceSummary Summary,
        IReadOnlyList<TestReference> Unknown
    ) Analyze(string root, IReadOnlyList<CatalogEntry> entries)
    {
        var references = new List<TestReference>();
        foreach (var file in SourceFiles(root))
        {
            references.AddRange(
                TraitScanner.Scan(RepositoryRoot.Relative(root, file), File.ReadAllText(file))
            );
        }

        var known = entries.Select(static e => e.Id).ToHashSet(StringComparer.Ordinal);
        var (markdown, summary) = TraceReport.Render(
            entries,
            references,
            ManualIdentifiers(root, known)
        );
        return (markdown, summary, [.. references.Where(r => !known.Contains(r.Id))]);
    }

    /// <summary>The C# files that may hold tests, in a stable order.</summary>
    private static List<string> SourceFiles(string root)
    {
        var files = new List<string>();
        foreach (var folder in TestRoots.Select(name => Path.Combine(root, name)))
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            files.AddRange(
                Directory
                    .EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                    .Where(file =>
                        !RepositoryRoot
                            .Relative(root, file)
                            .Split('/')
                            .Any(segment =>
                                SkippedFolders.Contains(segment, StringComparer.OrdinalIgnoreCase)
                            )
                    )
            );
        }

        files.Sort(
            (a, b) =>
                string.CompareOrdinal(
                    RepositoryRoot.Relative(root, a),
                    RepositoryRoot.Relative(root, b)
                )
        );
        return files;
    }

    /// <summary>The catalog identifiers the manual acceptance script names; none when the script does not exist.</summary>
    private static HashSet<string> ManualIdentifiers(string root, HashSet<string> known)
    {
        var path = Path.Combine(root, ManualScriptPath);
        var named = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return named;
        }

        foreach (Match match in IdentifierPattern().Matches(File.ReadAllText(path)))
        {
            if (known.Contains(match.Value))
            {
                named.Add(match.Value);
            }
        }

        return named;
    }

    [GeneratedRegex(
        @"\b(?:EC-[A-Z]{2,4}-\d{2}|[A-Z]{2,4}-\d{2,3})\b",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex IdentifierPattern();
}
