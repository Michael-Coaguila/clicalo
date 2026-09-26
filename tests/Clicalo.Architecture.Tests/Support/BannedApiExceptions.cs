using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Clicalo.Architecture.Tests.Support;

/// <summary><c>architecture/banned-api-exceptions.json</c>.</summary>
internal sealed record BannedApiExceptions(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    ImmutableArray<BannedApiException> Exceptions
);
