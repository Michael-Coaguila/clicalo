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
        await EndOf(pending);

        _world.Control.Attempts.ShouldBe([Word, Word]);
        Timings.Foreground.RestoreRetryDelay.ShouldBeLessThan(
            Timings.Windowing.ViolationRestoreBudget,
            "the retry fits inside the REG-01 restore budget"
        );
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
