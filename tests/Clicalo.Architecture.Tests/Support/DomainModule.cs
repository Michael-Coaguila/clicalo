using System.Collections.Immutable;

namespace Clicalo.Architecture.Tests.Support;

internal sealed record DomainModule(
    string Name,
    string Group,
    string Description,
    ImmutableArray<string> DependsOn,
    string? Deviation = null
);
