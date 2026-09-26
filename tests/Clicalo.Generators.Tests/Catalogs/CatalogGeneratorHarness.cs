using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Generators.Catalogs;
using Clicalo.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Generators.Tests.Catalogs;

/// <summary>
/// Runs <see cref="CatalogGenerator"/> in memory over catalog files, together with the hand-written Domain types the
/// generated code builds on (src/Clicalo.Domain/Keys, Catalog and Timing), exactly as the Domain build does.
/// </summary>
internal static class CatalogGeneratorHarness
{
    /// <summary>Folder that makes an additional file a catalog (the generator filters on data/catalogs).</summary>
    public static readonly string CatalogDirectory = Path.Combine(
        RepoPaths.Root,
        "artifacts",
        "virtual",
        "data",
        "catalogs"
    );

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(path =>
                    Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal)
                    || Path.GetFileName(path) is "mscorlib.dll" or "netstandard.dll"
                )
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ]
    );

    /// <summary>The implicit usings of the SDK, which the hand-written Domain files rely on.</summary>
    private const string ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    /// <summary>A catalog file under <see cref="CatalogDirectory"/>.</summary>
    public static AdditionalText Catalog(string fileName, string content) =>
        new MemoryText(Path.Combine(CatalogDirectory, fileName), content);

    /// <summary>The real catalogs of data/catalogs.</summary>
    public static IReadOnlyList<AdditionalText> RealCatalogs() =>
        [
            .. Directory
                .GetFiles(Path.Combine(RepoPaths.Data, "catalogs"), "*.json")
                .Order(StringComparer.Ordinal)
                .Select(path => (AdditionalText)new MemoryText(path, File.ReadAllText(path))),
        ];

    public static GeneratorRun Run(
        IEnumerable<AdditionalText> catalogs,
        string profile = "Domain"
    ) => Run(CreateDriver(profile).AddAdditionalTexts([.. catalogs]));

    public static GeneratorDriver CreateDriver(string profile) =>
        CSharpGeneratorDriver.Create(
            [new CatalogGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            optionsProvider: new ProfileOptions(profile),
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

    public static GeneratorRun Run(GeneratorDriver driver)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(
            DomainCompilation(),
            out var output,
            out var driverDiagnostics
        );
        return new GeneratorRun(driver, driver.GetRunResult(), output, driverDiagnostics);
    }

    /// <summary>Compilation of the hand-written Domain types the generated code needs.</summary>
    public static CSharpCompilation DomainCompilation()
    {
        var sources = new[] { "Keys", "Catalog", "Timing" }
            .SelectMany(folder =>
                Directory.GetFiles(
                    Path.Combine(RepoPaths.Root, "src", "Clicalo.Domain", folder),
                    "*.cs"
                )
            )
            .Order(StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, ParseOptions, "GlobalUsings.cs"));

        return CSharpCompilation.Create(
            "Clicalo.Domain.GeneratorTest",
            sources,
            References.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }

    /// <summary>A catalog file held in memory.</summary>
    private sealed class MemoryText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(content);
    }

    /// <summary>Exposes <c>build_property.ClicaloGeneratorProfile</c> like the Domain project file does.</summary>
    private sealed class ProfileOptions(string profile) : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global = new(profile);

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

        private sealed class Options(string? profile) : AnalyzerConfigOptions
        {
            public static readonly Options Empty = new(null);

            // MaybeNullWhen(false): NotNullWhen is ambiguous here because Clicalo.Generators polyfills it.
            public override bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
            {
                value = profile;
                return profile is not null
                    && string.Equals(
                        key,
                        "build_property.ClicaloGeneratorProfile",
                        StringComparison.Ordinal
                    );
            }
        }
    }
}

/// <summary>Result of one generator run.</summary>
internal sealed record GeneratorRun(
    GeneratorDriver Driver,
    GeneratorDriverRunResult Result,
    Compilation Output,
    ImmutableArray<Diagnostic> DriverDiagnostics
)
{
    /// <summary>Diagnostics reported by the generator (the CLCC data errors).</summary>
    public ImmutableArray<Diagnostic> Diagnostics => Result.Diagnostics;

    /// <summary>Generated source by hint name.</summary>
    public IReadOnlyDictionary<string, string> Sources =>
        Result
            .Results.SelectMany(r => r.GeneratedSources)
            .ToDictionary(s => s.HintName, s => s.SourceText.ToString(), StringComparer.Ordinal);

    /// <summary>Compiler warnings and errors of the Domain types plus the generated code.</summary>
    public IReadOnlyList<Diagnostic> CompilationProblems =>
        [.. Output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning)];
}
