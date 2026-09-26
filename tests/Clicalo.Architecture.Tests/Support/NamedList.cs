using System.Collections.Immutable;

namespace Clicalo.Architecture.Tests.Support;

internal sealed record NamedList(string Description, ImmutableArray<string> Projects);
