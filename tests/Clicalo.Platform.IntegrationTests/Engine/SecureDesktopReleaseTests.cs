using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.Input;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Releases the secure desktop refuses without locking the session (UAC, Ctrl+Alt+Del; blueprint §7.6, INV-3, D-22):
/// as soon as <see cref="InputDesktopWatch"/> sees the input desktop come back, the engine hears
/// <see cref="EngineEvent.SessionResumed"/> and sends them again. Nothing is injected: the desktop is a fake.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "REG-03")]
public sealed class SecureDesktopReleaseTests
{
    [Fact]
    public void Leaving_the_secure_desktop_tells_the_engine_at_once()
    {
        var world = new DesktopWorld { Reachable = true };

        world.Watch.OnDesktopSwitched();

        world.Inbox.Posted.ShouldHaveSingleItem().ShouldBeOfType<EngineEvent.SessionResumed>();
        world.Watch.IsWaiting.ShouldBeFalse();
    }

    [Fact]
    public void While_the_secure_desktop_is_in_front_it_is_checked_again_until_it_leaves()
    {
        var world = new DesktopWorld { Reachable = false };
        world.Watch.OnDesktopSwitched();
        world.Inbox.Posted.ShouldBeEmpty();

        world.Time.Advance(Timings.KeySafety.InputDesktopRecheck[0]);
        world.Inbox.Posted.ShouldBeEmpty();
        world.Reachable = true;
        world.Time.Advance(Timings.KeySafety.InputDesktopRecheck[1]);

        world.Inbox.Posted.ShouldHaveSingleItem().ShouldBeOfType<EngineEvent.SessionResumed>();
        world.Watch.IsWaiting.ShouldBeFalse();
        for (var i = 0; i < 100; i++)
        {
            world.Time.Advance(TimeSpan.FromMilliseconds(100));
        }

        world.Inbox.Posted.Count.ShouldBe(1);
    }

    [Fact]
    public void The_checks_after_one_switch_are_bounded()
    {
        var world = new DesktopWorld { Reachable = false };
        world.Watch.OnDesktopSwitched();

        for (var i = 0; i < 600; i++)
        {
            world.Time.Advance(TimeSpan.FromMilliseconds(100));
        }

        world.Checks.ShouldBe(1 + Timings.KeySafety.InputDesktopRecheck.Length);
        world.Watch.IsWaiting.ShouldBeFalse();
        world.Inbox.Posted.ShouldBeEmpty();

        // The switch back to Clícalo's desktop starts over.
        world.Reachable = true;
        world.Watch.OnDesktopSwitched();
        world.Inbox.Posted.ShouldHaveSingleItem();
    }

    private sealed class DesktopWorld
    {
        public DesktopWorld() =>
            Watch = new InputDesktopWatch(
                Inbox,
                () =>
                {
                    Checks++;
                    return Reachable;
                },
                Time
            );

        public FakeTimeProvider Time { get; } =
            new(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));

        public RecordingInbox Inbox { get; } = new();

        public InputDesktopWatch Watch { get; }

        public bool Reachable { get; set; }

        public int Checks { get; private set; }
    }

    private sealed class RecordingInbox : IEngineInbox
    {
        public List<EngineEvent> Posted { get; } = [];

        public bool Post(EngineEvent engineEvent)
        {
            Posted.Add(engineEvent);
            return true;
        }
    }
}
