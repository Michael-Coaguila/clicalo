using Clicalo.Domain.Primitives;

namespace Clicalo.Windowing.IntegrationTests.SearchPanel;

/// <summary>Ids «s1», «p2»… in order, for the search and suggestion tests.</summary>
internal sealed class SequentialTestIds : IIdGenerator
{
    private int _next;

    public ShortcutId NewShortcutId() => new("s" + ++_next);

    public ProfileId NewProfileId() => new("p" + ++_next);
}
