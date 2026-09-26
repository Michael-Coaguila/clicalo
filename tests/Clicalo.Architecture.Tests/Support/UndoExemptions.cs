using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Clicalo.Architecture.Tests.Support;

/// <summary><c>architecture/undo-exemptions.json</c>.</summary>
internal sealed record UndoExemptions(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    string Requirement,
    ImmutableArray<UndoExemption> Exemptions
);
