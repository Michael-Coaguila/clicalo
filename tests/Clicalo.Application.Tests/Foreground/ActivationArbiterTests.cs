using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using static Clicalo.Application.Tests.Foreground.ForegroundWorld;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// The orchestrator as <see cref="IActivationArbiter"/> for <c>ActivationGuard</c> (blueprint §3.5, deviation D-14):
/// an activation is legitimate only for the target of the lease being granted or active, and a reported violation
/// gives the foreground back to the last verified external window, or to the target of the active lease.
/// </summary>
[Trait("Req", "REG-01")]
public sealed class ActivationArbiterTests : IDisposable
{
    /// <summary>How long a test waits for an operation whose clock it has already moved past every wait.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    private readonly ForegroundWorld _world = new();

    public void Dispose() => _world.Dispose();

    private ForegroundOrchestrator Arbiter => _world.Orchestrator;

    [Fact]
    public void The_orchestrator_is_the_arbiter_that_ActivationGuard_talks_to() =>
        _world.Orchestrator.ShouldBeAssignableTo<IActivationArbiter>();

    [Fact]
    public void Without_a_lease_no_activation_is_leased()
    {
        Arbiter.IsActivationLeased(Search).ShouldBeFalse();
        Arbiter.IsActivationLeased(Panel).ShouldBeFalse();
        Arbiter.IsActivationLeased(WindowToken.None).ShouldBeFalse();
    }

    [Fact]
    public async Task Only_the_target_of_the_active_lease_is_leased()
    {
        _world.Control.HasRights = true;

        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);

        Arbiter.IsActivationLeased(Search).ShouldBeTrue();
        Arbiter.IsActivationLeased(Panel).ShouldBeFalse("another surface is still a violation");
        Arbiter.IsActivationLeased(Word).ShouldBeFalse();
        _ = await search.RestoreAsync(TestContext.Current.CancellationToken);
        Arbiter.IsActivationLeased(Search).ShouldBeFalse();
    }

    [Fact]
    public async Task The_target_is_already_leased_while_it_comes_to_the_front()
    {
        _world.Control.HasRights = true;
        var leasedDuringAttempt = false;
        var activatableDuringAttempt = false;
        _world.Control.DuringAttempt = window =>
        {
            leasedDuringAttempt = Arbiter.IsActivationLeased(window);
            activatableDuringAttempt = _world.Surfaces.Activatable.Contains(SearchSurface);
        };

        _ = await _world.GrantAsync(LeaseKind.TextInput, Search);

        leasedDuringAttempt.ShouldBeTrue("WM_ACTIVATE arrives while SetForegroundWindow runs");
        activatableDuringAttempt.ShouldBeTrue();
    }

    [Fact]
    public async Task While_a_replacement_is_being_granted_both_targets_are_leased()
    {
        _world.Control.HasRights = true;
        _ = await _world.GrantAsync(LeaseKind.TextInput, Search);
        var searchLeased = false;
        _world.Control.DuringAttempt = _ => searchLeased = Arbiter.IsActivationLeased(Search);

        _ = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);

        searchLeased.ShouldBeTrue();
        Arbiter.IsActivationLeased(Search).ShouldBeFalse();
        Arbiter.IsActivationLeased(ControlCenter).ShouldBeTrue();
    }

    [Fact]
    public async Task A_violation_gives_the_foreground_back_to_the_last_verified_external_window()
    {
        // The panel was activated from outside: it owns the foreground now, so Clícalo may give it back.
        _world.Control.Foreground = Panel;

        Arbiter.ReportViolation(Violation(Panel, PanelSurface));
        await _world.Orchestrator.ViolationRestore;

        _world.Control.Attempts.ShouldBe([Word]);
        _world.Control.Foreground.ShouldBe(Word);
    }

    [Fact]
    public async Task A_violation_restore_is_verified_with_one_retry()
    {
        _world.Control.Foreground = Panel;
        _world.Control.Script.Enqueue(false);
        _world.Control.Script.Enqueue(true);

        var pending = await _world.StartUntilItWaitsAsync(() =>
            _world
                .Orchestrator.RestoreAfterViolationAsync(
                    Word,
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);
        await pending.WaitAsync(OperationTimeout, TestContext.Current.CancellationToken);

        _world.Control.Attempts.ShouldBe([Word, Word]);
        Timings.Foreground.RestoreRetryDelay.ShouldBeLessThan(
            Timings.Windowing.ViolationRestoreBudget,
            "the retry fits inside the REG-01 restore budget"
        );
    }

    [Fact]
    public async Task A_restore_that_reached_the_window_is_not_retried_once_the_foreground_moves_on()
    {
        // Spike S1 in CI (OrchestratedRestoreTests): GetForegroundWindow did not confirm the restore at once, InputProbe
        // got the foreground back a moment later, and the next forced activation put the panel in front before the
        // single look at the end of the wait. That look judged the restore refused, and its retry took the foreground
        // back before the panel saw itself in front, so ActivationGuard never counted that activation.
        _world.Control.Foreground = Panel;
        _world.Control.Script.Enqueue(false);
        var pending = await _world.StartUntilItWaitsAsync(() =>
            _world
                .Orchestrator.RestoreAfterViolationAsync(
                    Word,
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );

        _world.Control.Foreground = Word;
        _world.Time.Advance(Timings.Foreground.RestoreVerifyInterval);
        _world.Control.Foreground = Panel;
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);
        await pending.WaitAsync(OperationTimeout, TestContext.Current.CancellationToken);

        _world.Control.Attempts.ShouldBe(
            [Word],
            "the restore was confirmed when Word came back; the new activation is the guard's to report"
        );
        _world.Control.Foreground.ShouldBe(Panel);
    }

    [Fact]
    public async Task A_restore_is_confirmed_by_the_monitor_even_if_the_foreground_moved_on_between_two_looks()
    {
        // Spike S1 in CI (s0 37171041134): Windows timers run the looks 15.6 ms apart, InputProbe was back and the panel
        // forced again between two of them, and the retry at the end of the wait reverted that activation before
        // ActivationGuard saw it. The monitor had verified InputProbe in front meanwhile.
        _world.Control.Foreground = Panel;
        _world.Control.Script.Enqueue(false);
        var pending = await _world.StartUntilItWaitsAsync(() =>
            _world
                .Orchestrator.RestoreAfterViolationAsync(
                    Word,
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );

        _world.Monitor.SwitchTo(Word);
        _world.Control.Foreground = Panel;
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);
        await pending.WaitAsync(OperationTimeout, TestContext.Current.CancellationToken);

        _world.Control.Attempts.ShouldBe([Word], "the monitor's report confirmed the restore");
        _world.Control.Foreground.ShouldBe(Panel);
    }

    [Fact]
    public async Task A_restore_is_never_retried_over_an_app_the_user_switched_to()
    {
        _world.Control.Foreground = Panel;
        _world.Control.Script.Enqueue(false);
        var pending = await _world.StartUntilItWaitsAsync(() =>
            _world
                .Orchestrator.RestoreAfterViolationAsync(
                    Word,
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );

        _world.Monitor.SwitchTo(Notepad);
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);
        await pending.WaitAsync(OperationTimeout, TestContext.Current.CancellationToken);

        _world.Control.Attempts.ShouldBe([Word], "the user chose Notepad meanwhile");
        _world.Control.Foreground.ShouldBe(Notepad);
    }

    [Fact]
    public async Task A_violation_restore_that_waited_for_another_does_nothing_once_the_app_is_back()
    {
        // Spike S1 in CI (OrchestratedRestoreTests, cycles 18 to 20): the restore of cycle 19 waited behind the retry of
        // cycle 18, InputProbe was back meanwhile, and when its turn came it took the foreground from the forced
        // activation of cycle 20 before ActivationGuard saw the panel in front.
        _world.Control.Foreground = Panel;
        _world.Control.Script.Enqueue(false);
        var timers = _world.Time.TimersCreated;
        Arbiter.ReportViolation(Violation(Panel, PanelSurface));
        await _world.Time.WhenTimersAsync(timers + 1);
        Arbiter.ReportViolation(Violation(Panel, PanelSurface));
        var queued = _world.Orchestrator.ViolationRestore;

        _world.Monitor.SwitchTo(Word);
        _world.Time.Advance(Timings.Foreground.RestoreVerifyInterval);
        await queued.WaitAsync(OperationTimeout, TestContext.Current.CancellationToken);

        _world.Control.Attempts.ShouldBe(
            [Word],
            "Word was verified in front after the second report: that violation is over"
        );
    }

    [Fact]
    public async Task A_violation_restore_that_waited_still_runs_while_a_surface_is_in_front()
    {
        // The first restore is refused twice, so the panel stays in front all along.
        _world.Control.Foreground = Panel;
        _world.Control.Script.Enqueue(false);
        _world.Control.Script.Enqueue(false);
        var timers = _world.Time.TimersCreated;
        Arbiter.ReportViolation(Violation(Panel, PanelSurface));
        await _world.Time.WhenTimersAsync(timers + 1);
        Arbiter.ReportViolation(Violation(Panel, PanelSurface));
        var queued = _world.Orchestrator.ViolationRestore;

        // Word was verified in front for a moment meanwhile, but the panel is in front when the queued restore runs.
        _world.Monitor.SwitchTo(Word, alsoForeground: false);
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);
        await _world.Time.WhenTimersAsync(timers + 2);
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);
        await queued.WaitAsync(OperationTimeout, TestContext.Current.CancellationToken);

        _world.Control.Attempts.ShouldBe([Word, Word, Word]);
        _world.Control.Foreground.ShouldBe(Word);
    }

    [Fact]
    public async Task A_violation_on_a_leased_window_is_ignored()
    {
        _world.Control.HasRights = true;
        _ = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Mark();

        Arbiter.ReportViolation(Violation(Search, SearchSurface));

        _world.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_violation_during_a_lease_gives_the_foreground_back_to_its_target_and_keeps_it()
    {
        _world.Control.HasRights = true;
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Control.Foreground = Panel;
        _world.Mark();

        Arbiter.ReportViolation(Violation(Panel, PanelSurface));
        await _world.Orchestrator.ViolationRestore;

        _world.Log.ShouldBe(["set Search"], "the search keeps the foreground, not Word");
        _world.Control.Foreground.ShouldBe(Search);
        search.IsActive.ShouldBeTrue("a violation elsewhere does not end the text input");
        Arbiter.IsActivationLeased(Search).ShouldBeTrue();
    }

    [Fact]
    public async Task Nothing_is_restored_without_a_known_external_foreground()
    {
        using var world = new ForegroundWorld(seedWord: false);

        world.Orchestrator.ReportViolation(Violation(Panel, PanelSurface));
        await world.Orchestrator.ViolationRestore;
        await world.Orchestrator.RestoreAfterViolationAsync(
            WindowToken.None,
            TestContext.Current.CancellationToken
        );

        world.Control.Attempts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reporting_a_violation_returns_before_the_foreground_is_touched()
    {
        // ActivationGuard reports from inside the surface's window procedure: nothing may happen on that thread.
        _world.Control.Foreground = Panel;
        var reporter = Environment.CurrentManagedThreadId;
        var reporting = true;
        var attemptedInsideTheReport = false;
        _world.Control.DuringAttempt = _ =>
            attemptedInsideTheReport |=
                Volatile.Read(ref reporting) && Environment.CurrentManagedThreadId == reporter;

        Arbiter.ReportViolation(Violation(Panel, PanelSurface));
        Volatile.Write(ref reporting, false);
        await _world.Orchestrator.ViolationRestore;

        attemptedInsideTheReport.ShouldBeFalse();
        _world.Control.Attempts.ShouldBe([Word]);
        _world.Control.Foreground.ShouldBe(Word);
    }

    private ActivationViolation Violation(WindowToken window, SurfaceId surface) =>
        new(
            surface,
            window,
            ActivationMessage.Activate,
            ActivationCause.External,
            _world.Time.GetUtcNow()
        );
}
