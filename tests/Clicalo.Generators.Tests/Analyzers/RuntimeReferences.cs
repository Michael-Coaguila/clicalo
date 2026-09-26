using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Clicalo.Generators.Tests.Analyzers;

/// <summary>
/// Compiles analyzer test code against the .NET runtime that runs the tests, so the tests never download reference
/// packages and give the same result offline, locally and in CI.
/// </summary>
internal static class RuntimeReferences
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> Loaded = new(Load);

    /// <summary>A reference set that resolves nothing by itself; <see cref="Framework"/> supplies the assemblies.</summary>
    public static ReferenceAssemblies None { get; } = new("net10.0");

    /// <summary>Every managed assembly of the shared framework the tests run on.</summary>
    public static ImmutableArray<MetadataReference> Framework => Loaded.Value;

    private static ImmutableArray<MetadataReference> Load()
    {
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return
        [
            .. trusted
                .Split(Path.PathSeparator)
                .Where(path =>
                    string.Equals(
                        Path.GetDirectoryName(path),
                        frameworkDirectory,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .Order(StringComparer.Ordinal)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ];
    }
}
