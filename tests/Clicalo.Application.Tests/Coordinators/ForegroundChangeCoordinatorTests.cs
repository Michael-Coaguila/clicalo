using System.Collections.Immutable;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.TestKit.Time;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>
/// Step 1 of the app switch (blueprint §7.9): every verified external foreground reaches the engine with a new epoch,
/// and only a change of app is a user switch that releases what is held (SEG-005).
/// </summary>
[Trait("Req", "PER-003")]
public sealed class ForegroundChangeCoordinatorTests : IDisposable
{
    private static readonly KeyboardLayoutSnapshot Spanish = new(
        new KeyboardLayoutId(0x040A_040A),
        ImmutableDictionary<KeyId, LayoutKey>.Empty
    );

    private readonly ScriptedForegroundMonitor _monitor = new();
    private readonly RecordingEngineInbox _engine = new();
    private readonly ForegroundChangeCoordinator _coordinator;
    private DateTimeOffset _now = TestTime.Epoch;

    public ForegroundChangeCoordinatorTests() =>
        _coordinator = new ForegroundChangeCoordinator(
            _monitor,
            _engine,
            foreground => new ForegroundDetails(
                new ProcessName("app" + foreground.AppProcessId + ".exe"),
                Spanish
            ),
            selfElevated: false
        );

    public void Dispose() => _coordinator.Dispose();

    [Fact]
    public void Starting_posts_the_foreground_already_known_as_the_first_epoch()
    {
        _monitor.Seed(Window(0x10, process: 100));

        _coordinator.Start();

        var change = Changes().ShouldHaveSingleItem();
        change.IsUserSwitch.ShouldBeFalse("nothing was held before the first foreground");
        change.Info.ShouldBe(
            new ForegroundInfo(
                new ForegroundWindowId(0x10),
                new ProcessName("app100.exe"),
                1,
                ElevationState.Allowed,
                Spanish
            )
        );
        change.Lane.ShouldBe(EngineLane.Priority);
        _coordinator.CurrentEpoch.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "SEG-005")]
    public void A_change_of_app_is_a_user_switch_with_a_new_epoch()
    {
        _coordinator.Start();
        _monitor.SwitchTo(Window(0x10, process: 100));

        _monitor.SwitchTo(Window(0x20, process: 200));

        Changes()
            .Select(change => (change.Info.Epoch, change.IsUserSwitch))
            .ShouldBe([(1L, false), (2L, true)]);
        _coordinator.CurrentEpoch.ShouldBe(2);
    }

    [Fact]
    [Trait("Req", "SEG-005")]
    public void Coming_back_to_the_same_app_after_a_lease_keeps_what_is_held()
    {
        _coordinator.Start();
        _monitor.SwitchTo(Window(0x10, process: 100));

        // The tray menu or the Control Center was in front: the monitor reports the app again.
        _monitor.SwitchTo(Window(0x10, process: 100));
        _monitor.SwitchTo(Window(0x11, process: 100));

        Changes().Select(change => change.IsUserSwitch).ShouldBe([false, false, false]);
        _coordinator.CurrentEpoch.ShouldBe(
            3,
            "every reported foreground gets its own epoch (INV-6)"
        );
    }

    [Fact]
    [Trait("Req", "PRB-006")]
    public void The_switches_of_a_try_and_the_way_back_are_not_user_switches()
    {
        var trying = false;
        _coordinator.IsTrying = () => trying;
        _coordinator.Start();
        _monitor.SwitchTo(Window(0x10, process: 100));

        trying = true;
        _monitor.SwitchTo(Window(0x20, process: 200));
        trying = false;
        // The Control Center closes and gives the foreground back to the app the user was in.
        _monitor.SwitchTo(Window(0x10, process: 100));

        Changes()
            .Select(change => (change.Info.Epoch, change.IsUserSwitch))
            .ShouldBe([(1L, false), (2L, false), (3L, false)]);
    }

    [Fact]
    public void A_repeated_report_of_the_same_observation_is_posted_once()
    {
        var word = Window(0x10, process: 100);
        _monitor.Seed(word);
        _coordinator.Start();

        _monitor.SwitchTo(word);

        Changes().Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(ProcessElevation.NotElevated, false, ElevationState.Allowed)]
    [InlineData(ProcessElevation.Elevated, false, ElevationState.TargetElevated)]
    [InlineData(ProcessElevation.Elevated, true, ElevationState.Allowed)]
    [InlineData(ProcessElevation.Unknown, false, ElevationState.Unknown)]
    [InlineData(ProcessElevation.Unknown, true, ElevationState.Unknown)]
    [Trait("Req", "EJE-013")]
    public void An_elevated_app_blocks_sending_only_while_Clicalo_is_not_elevated(
        ProcessElevation target,
        bool selfElevated,
        ElevationState expected
    ) => ForegroundChangeCoordinator.ElevationOf(target, selfElevated).ShouldBe(expected);

    [Fact]
    public void Starting_twice_and_disposing_leave_no_subscription_behind()
    {
        _coordinator.Start();
        _coordinator.Start();
        _monitor.Subscribers.ShouldBe(1);

        _coordinator.Dispose();

        _monitor.Subscribers.ShouldBe(0);
    }

    private List<EngineEvent.ForegroundChanged> Changes() =>
        [.. _engine.Events.OfType<EngineEvent.ForegroundChanged>()];

    private ExternalForeground Window(nint handle, uint process)
    {
        _now = _now.AddSeconds(1);
        return new ExternalForeground(new WindowToken(handle), process, process + 1, _now)
        {
            Elevation = ProcessElevation.NotElevated,
        };
    }
}
