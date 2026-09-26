using System.Collections.Immutable;
using Clicalo.Generators.Localization;
using Clicalo.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Generators.Tests.Localization;

/// <summary>Runs <see cref="LocalizationGenerator"/> in memory, as the Domain project would.</summary>
internal static class GeneratorHarness
{
    public const string DataDirectory = "/repo/data/i18n/";

    private const string ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(
        LoadReferences
    );

    /// <summary>Runs the generator over <paramref name="files"/> (file name → content) under <c>data/i18n</c>.</summary>
    public static GeneratorOutput Run(
        IReadOnlyDictionary<string, string> files,
        string? profile = "Domain"
    )
    {
        var compilation = DomainCompilation();
        var driver = CreateDriver(files, profile)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return new GeneratorOutput(driver.GetRunResult(), output);
    }

    /// <summary>
    /// A driver over <paramref name="files"/> that records its pipeline steps, for incremental-generation tests.
    /// </summary>
    public static GeneratorDriver CreateDriver(
        IReadOnlyDictionary<string, string> files,
        string? profile = "Domain"
    ) =>
        CSharpGeneratorDriver.Create(
            [new LocalizationGenerator().AsSourceGenerator()],
            [
                .. files.Select(f =>
                    (AdditionalText)new InMemoryAdditionalText(DataDirectory + f.Key, f.Value)
                ),
            ],
            ParseOptions,
            new ProfileOptionsProvider(profile),
            new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

    /// <summary>The Domain compilation the generator runs against (the hand-written message types).</summary>
    public static CSharpCompilation DomainCompilation() =>
        CSharpCompilation.Create(
            "Clicalo.Domain",
            DomainMessageSources(),
            References.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

    /// <summary>The parse options of the Domain compilation, for trees that tests add to it.</summary>
    public static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Preview);

    /// <summary>The hand-written Domain message types the generated code builds on, with the SDK implicit usings.</summary>
    private static IEnumerable<SyntaxTree> DomainMessageSources()
    {
        var options = ParseOptions;
        yield return CSharpSyntaxTree.ParseText(ImplicitUsings, options, "ImplicitUsings.g.cs");
        foreach (
            var path in Directory
                .EnumerateFiles(RepoPaths.Combine("src", "Clicalo.Domain", "Messages"), "*.cs")
                .Order(StringComparer.Ordinal)
        )
        {
            yield return CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path);
        }
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(
            Path.PathSeparator
        );
        return
        [
            .. paths
                .Where(static p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(static p => MetadataReference.CreateFromFile(p)),
        ];
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(text);
    }

    private sealed class ProfileOptionsProvider(string? profile) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(profile);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(null);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            new Options(null);
    }

    // Nullable-oblivious on purpose: the NotNullWhen polyfill of Clicalo.Generators is visible here through
    // InternalsVisibleTo and clashes with the BCL attribute, so the override cannot repeat the annotation.
#nullable disable
    private sealed class Options(string profile) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = string.Equals(
                key,
                "build_property.ClicaloGeneratorProfile",
                StringComparison.Ordinal
            )
                ? profile
                : null;
            return value is not null;
        }
    }
#nullable restore
}
