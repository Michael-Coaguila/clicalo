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

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    /// <summary>Runs the generator over <paramref name="files"/> (file name → content) under <c>data/tokens</c>.</summary>
    public static GeneratorDriverRunResult Run(
        IReadOnlyDictionary<string, string> files,
        string? profile = "UiWpf"
    )
    {
        var texts = files
            .Select(file =>
                (AdditionalText)
                    new InMemoryAdditionalText(TokenTestData.Directory + file.Key, file.Value)
            )
            .ToImmutableArray();
        var compilation = CSharpCompilation.Create(
            "Clicalo.UI.Wpf",
            [],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TokenGenerator().AsSourceGenerator()],
            texts,
            ParseOptions,
            new ProfileOptionsProvider(profile)
        );
        return driver.RunGenerators(compilation).GetRunResult();
    }

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
