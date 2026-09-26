using System.Collections.Immutable;

namespace Clicalo.Architecture.Tests.Support;

internal sealed record BannedApiException(
    string Id,
    ImmutableArray<string> Paths,
    ImmutableArray<string> Apis,
    string Justification,
    string Blueprint
);
