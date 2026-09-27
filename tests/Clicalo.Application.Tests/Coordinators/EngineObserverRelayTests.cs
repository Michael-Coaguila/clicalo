using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.TestKit.Time;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>
/// The engine's outputs reach the Surfaces role only through its queue, and snapshots are coalesced so the UI paints
/// the newest one at most once per turn (blueprint §3.2 rule 5; the panic strip follows them, SEG-002).
/// </summary>
[Trait("Req", "SEG-002")]
public sealed class EngineObserverRelayTests
{
    private readonly Queue<Action> _surfaces = new();
    private readonly EngineObserverRelay _relay;
    private readonly List<long> _painted = [];

    public EngineObserverRelayTests()
    {
        _relay = new EngineObserverRelay(_surfaces.Enqueue);
        _relay.SnapshotChanged += (_, change) => _painted.Add(change.Snapshot.Version);
    }

    [Fact]
    public void Snapshots_published_before_the_ui_turn_are_painted_once_with_the_newest()
    {
        _relay.OnSnapshot(Snapshot(1));
        _relay.OnSnapshot(Snapshot(2));
        _relay.OnSnapshot(Snapshot(3));

        _painted.ShouldBeEmpty("nothing runs on the engine thread");
        _surfaces.Count.ShouldBe(1);
        RunSurfaces();

        _painted.ShouldBe([3L]);
        _relay.Latest.Version.ShouldBe(3);
    }

    [Fact]
    public void A_snapshot_after_the_ui_turn_is_painted_in_the_next_one()
    {
        _relay.OnSnapshot(Snapshot(1));
        RunSurfaces();

        _relay.OnSnapshot(Snapshot(2));
        RunSurfaces();

        _painted.ShouldBe([1L, 2L]);
    }

    [Fact]
    public void A_snapshot_published_while_the_ui_paints_is_not_lost()
    {
        _relay.SnapshotChanged += (_, change) =>
        {
            if (change.Snapshot.Version == 1)
            {
                _relay.OnSnapshot(Snapshot(2));
            }
        };
        _relay.OnSnapshot(Snapshot(1));

        RunSurfaces();

        _painted.ShouldBe([1L, 2L]);
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Notices_and_usage_are_raised_on_the_surfaces_role()
    {
        var notices = new List<(Message, NoticeUrgency)>();
        var usage = new List<(ShortcutId, DateTimeOffset)>();
        _relay.NoticeRaised += (_, notice) => notices.Add((notice.Text, notice.Urgency));
        _relay.UsageCounted += (_, counted) => usage.Add((counted.Shortcut, counted.At));

        _relay.OnNotice(L.ReleasedAll, NoticeUrgency.Polite);
        _relay.OnUsage(new ShortcutId("copy"), TestTime.Epoch);
        _relay.OnLastAction(new ShortcutId("copy"));

        notices.ShouldBeEmpty();
        usage.ShouldBeEmpty();
        RunSurfaces();
        notices.ShouldBe([(L.ReleasedAll, NoticeUrgency.Polite)]);
        usage.ShouldBe([(new ShortcutId("copy"), TestTime.Epoch)]);
        _relay.LastAction.ShouldBe(new ShortcutId("copy"));
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    [Trait("Req", "SEG-007")]
    public async Task The_end_of_the_session_learns_that_the_engine_released_everything_without_the_ui_thread()
    {
        _relay.OnSnapshot(Snapshot(1) with { Held = Holding() });

        var released = _relay.WhenNothingHeldAsync(TestContext.Current.CancellationToken);
        released.IsCompleted.ShouldBeFalse("something is still held");
        _relay.OnSnapshot(Snapshot(2) with { Held = Holding() });
        released.IsCompleted.ShouldBeFalse("still held");
        _relay.OnSnapshot(Snapshot(3));

        await released.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _painted.ShouldBeEmpty("no Surfaces turn was needed");
        _relay
            .WhenNothingHeldAsync(TestContext.Current.CancellationToken)
            .IsCompleted.ShouldBeTrue("nothing held: at once");
    }

    private static ValueList<PressedItem> Holding()
    {
        var shortcut = new ShortcutId("shift");
        return new ValueList<PressedItem>([
            new PressedItem(
                HolderId.ForToggle(shortcut),
                HoldOrigin.Toggle,
                shortcut,
                null,
                [],
                MouseButtons.None,
                0,
                null
            ),
        ]);
    }

    private static EngineSnapshot Snapshot(long version) =>
        EngineSnapshot.Empty with
        {
            Version = version,
        };

    private void RunSurfaces()
    {
        while (_surfaces.TryDequeue(out var work))
        {
            work();
        }
    }
}
