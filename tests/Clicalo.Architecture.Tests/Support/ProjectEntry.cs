using System.Collections.Immutable;

namespace Clicalo.Architecture.Tests.Support;

internal sealed record ProjectEntry(
    string Area,
    string Rule,
    string Platform,
    ImmutableArray<string> ProjectReferences,
    ImmutableArray<string> PackageReferences,
    ImmutableArray<string>? FrameworkReferences = null,
    ImmutableArray<string>? AssemblyReferences = null
);
