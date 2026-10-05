using Clicalo.Application.Session;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Session;

/// <summary>
/// The session has a single writer, the Surfaces role that created the store (blueprint §3.2 rule 2, ADR-0003); any
/// thread may read it.
/// </summary>
/// <remarks>Each test creates its store on its own thread: xUnit may construct the class on another one.</remarks>
public sealed class SessionStoreTests
{
    private static SessionStore NewStore() => new(PanelSession.Initial(ProfileId.General));

    [Fact]
    [Trait("Req", "BUR-003")]
    public void A_change_is_published_and_announced_with_both_sessions()
    {
        var store = NewStore();
        var changes = new List<(PanelPresence Before, PanelPresence After)>();
        store.Changed += (_, change) =>
            changes.Add((change.Previous.Presence, change.Current.Presence));

        store.Dispatch(new SessionAction.Hide()).ShouldBeTrue();

        store.Current.Presence.ShouldBe(PanelPresence.Hidden);
        changes.ShouldBe([(PanelPresence.Visible, PanelPresence.Hidden)]);
    }

    [Fact]
    public void An_action_without_effect_announces_nothing()
    {
        var store = NewStore();
        var changes = 0;
        store.Changed += (_, _) => changes++;

        store.Dispatch(new SessionAction.Show()).ShouldBeFalse();

        changes.ShouldBe(0);
    }

    [Fact]
    public void Only_the_thread_that_created_the_store_may_write_it_while_any_thread_reads_it()
    {
        var store = NewStore();
        Exception? failure = null;
        PanelSession? read = null;
        var other = new Thread(() =>
        {
            read = store.Current;
            failure = Record.Exception(() => store.Dispatch(new SessionAction.Hide()));
        });

        other.Start();
        other.Join();

        read.ShouldBe(PanelSession.Initial(ProfileId.General));
        failure.ShouldBeOfType<InvalidOperationException>();
        store.CheckAccess().ShouldBeTrue();
        store.Current.Presence.ShouldBe(PanelPresence.Visible);
    }
}
