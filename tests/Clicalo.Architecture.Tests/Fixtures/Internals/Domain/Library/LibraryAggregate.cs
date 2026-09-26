using Clicalo.Architecture.Tests.Fixtures.Internals.Domain.Library.Internal;

namespace Clicalo.Architecture.Tests.Fixtures.Internals.Domain.Library;

/// <summary>Fixture: the Library module uses its own internals, which is allowed.</summary>
public sealed class LibraryAggregate
{
    private readonly ShortcutIndex _index = new();

    public int Count => _index.Count;
}
