using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Clicalo.Architecture.Tests.Support;

/// <summary><c>architecture/allowed-dependencies.json</c>.</summary>
internal sealed record AllowedDependencies(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    NamedList AnalyzerProjects,
    NamedPackages GlobalPackages,
    ImmutableSortedDictionary<string, ProjectEntry> Projects
);
