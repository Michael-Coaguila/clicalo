using System.Collections.Immutable;

namespace Clicalo.Architecture.Tests.Support;

internal sealed record NamedPackages(string Description, ImmutableArray<string> Packages);
