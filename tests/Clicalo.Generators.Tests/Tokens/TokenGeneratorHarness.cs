using System.Collections.Immutable;
using System.Text;
using Clicalo.Generators.Tokens;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Generators.Tests.Tokens;

/// <summary>Runs <see cref="TokenGenerator"/> in memory, as the UI.Wpf project would, and compiles its output.</summary>
internal static class TokenGeneratorHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(
        LoadReferences
    );

    /// <summary>The parse options of the generated code, for trees that tests add to the compilation.</summary>
    public static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Latest);

    /// <summary>Runs the generator over <paramref name="files"/> (file name → content) under <c>data/tokens</c>.</summary>
    public static GeneratorDriverRunResult Run(
        IReadOnlyDictionary<string, string> files,
        string? profile = "UiWpf"
    ) => CreateDriver(files, profile).RunGenerators(UiCompilation()).GetRunResult();

    /// <summary>
    /// A driver over <paramref name="files"/> that records its pipeline steps, for incremental-generation tests.
    /// </summary>
    public static GeneratorDriver CreateDriver(
        IReadOnlyDictionary<string, string> files,
        string? profile = "UiWpf"
    ) =>
        CSharpGeneratorDriver.Create(
            [new TokenGenerator().AsSourceGenerator()],
            [
                .. files.Select(file =>
                    (AdditionalText)
                        new InMemoryAdditionalText(TokenTestData.Directory + file.Key, file.Value)
                ),
            ],
            ParseOptions,
            new ProfileOptionsProvider(profile),
            new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

    /// <summary>The (empty) UI.Wpf compilation the generator runs against.</summary>
    public static CSharpCompilation UiCompilation() =>
        CSharpCompilation.Create(
            "Clicalo.UI.Wpf",
            [],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    /// <summary>Compiles generated trees together with minimal WPF stand-ins (the test host has no WPF).</summary>
    public static CSharpCompilation CompileWithWpfStubs(IEnumerable<SyntaxTree> generated) =>
        CSharpCompilation.Create(
            "GeneratedTokens",
            [
                .. generated,
                CSharpSyntaxTree.ParseText(WpfStubs.Source, ParseOptions, "WpfStubs.cs"),
            ],
            References.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(
            Path.PathSeparator
        );
        return
        [
            .. paths
                .Where(static path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(static path => MetadataReference.CreateFromFile(path)),
        ];
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(text, Encoding.UTF8);
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
