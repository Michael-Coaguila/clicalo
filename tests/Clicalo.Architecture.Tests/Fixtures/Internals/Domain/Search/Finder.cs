using Clicalo.Architecture.Tests.Fixtures.Internals.Domain.Library.Internal;

namespace Clicalo.Architecture.Tests.Fixtures.Internals.Domain.Search;

/// <summary>Fixture: the Search module reaches into the internals of Library, which is forbidden.</summary>
public sealed class Finder
{
    private readonly ShortcutIndex _index = new();

    public int Find() => _index.Count;
}
