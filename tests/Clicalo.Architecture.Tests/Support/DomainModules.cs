using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Clicalo.Architecture.Tests.Support;

/// <summary><c>architecture/domain-modules.json</c>.</summary>
internal sealed record DomainModules(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    string RootNamespace,
    ImmutableArray<DomainModule> Modules
);
