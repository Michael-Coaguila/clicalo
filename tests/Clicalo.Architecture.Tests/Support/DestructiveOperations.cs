using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Clicalo.Architecture.Tests.Support;

/// <summary><c>architecture/destructive-operations.json</c>.</summary>
internal sealed record DestructiveOperations(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    string Requirement,
    ImmutableArray<Operation> Commands,
    ImmutableArray<Operation> UseCases
);
