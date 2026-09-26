using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Clicalo.Architecture.Tests.Support;

/// <summary><c>architecture/sensitive-paths.json</c>.</summary>
internal sealed record SensitivePaths(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    ImmutableArray<SensitivePath> Paths
);
