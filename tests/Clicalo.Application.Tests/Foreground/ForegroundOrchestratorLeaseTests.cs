using Clicalo.Application.Foreground;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Time;
using static Clicalo.Application.Tests.Foreground.ForegroundWorld;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// The concurrency rules of the leases (blueprint §3.6): a single active lease; a new one replaces it without
/// restoring in between and inherits its previous foreground; <see cref="LeaseKind.TrayMenu"/> has priority and is
/// never replaced; a change of epoch, the idle timeout of text input and a terminal event end a lease.
/// </summary>
[Trait("Req", "REG-01")]
public sealed class ForegroundOrchestratorLeaseTests : IDisposable
{
    private readonly ForegroundWorld _world = new();

    public ForegroundOrchestratorLeaseTests() => _world.Control.HasRights = true;

    public void Dispose() => _world.Dispose();

    [Fact]
    [Trait("Req", "BUS-002")]
    public async Task A_lease_remembers_the_external_foreground_it_will_return_to()
    {
        var lease = await _world.GrantAsync(LeaseKind.TextInput, Search);

        lease.PreviousForeground.ShouldBe(Word);
        lease.Target.ShouldBe(Search);
        lease.Kind.ShouldBe(LeaseKind.TextInput);
        lease.EpochAtAcquire.ShouldBe(_world.Orchestrator.Current.Epoch);
        lease.IsActive.ShouldBeTrue();
        _world.Orchestrator.ActiveLease.ShouldBeSameAs(lease);
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    public async Task A_new_lease_replaces_the_active_one_and_inherits_its_previous_foreground()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Mark();

        var controlCenter = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);

        controlCenter.PreviousForeground.ShouldBe(Word, "not the search surface that was in front");
        search.IsActive.ShouldBeFalse();
        search.EndReason.ShouldBe(LeaseEndReason.Replaced);
        (await search.Ended).ShouldBe(LeaseEndReason.Replaced);
        _world.Log.ShouldBe(["set ControlCenter", "noactivate " + SearchSurface]);
        _world.Control.Attempts.ShouldNotContain(Word, "nothing is restored between two leases");
        _world.Orchestrator.ActiveLease.ShouldBeSameAs(controlCenter);
    }

    [Fact]
    public async Task A_replaced_lease_never_takes_the_foreground_back()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _ = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);
        _world.Mark();

        var outcome = await search.RestoreAsync(TestContext.Current.CancellationToken);
        await search.DisposeAsync();

        outcome.ShouldBe(RestoreOutcome.Failed);
        _world.Log.ShouldBeEmpty();
        _world.Control.Foreground.ShouldBe(ControlCenter);
    }

    [Fact]
    public async Task Replacing_a_text_lease_on_the_same_surface_keeps_it_activatable()
    {
        _ = await _world.GrantAsync(LeaseKind.TextInput, Search);

        var second = await _world.GrantAsync(LeaseKind.TextInput, Search);

        second.PreviousForeground.ShouldBe(Word);
        _world.Surfaces.Activatable.ShouldBe([SearchSurface]);
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public async Task The_tray_menu_replaces_any_lease_and_denies_every_other_request()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        var tray = await _world.GrantAsync(LeaseKind.TrayMenu, TrayHost, LeaseOrigin.Tray);
        _world.Mark();

        (await _world.DenyAsync(LeaseKind.ControlCenter, ControlCenter, LeaseOrigin.Tray)).ShouldBe(
            ForegroundDenialReason.TrayMenuActive
        );
        (await _world.DenyAsync(LeaseKind.TrayMenu, TrayHost, LeaseOrigin.Tray)).ShouldBe(
            ForegroundDenialReason.TrayMenuActive
        );

        search.EndReason.ShouldBe(LeaseEndReason.Replaced);
        tray.PreviousForeground.ShouldBe(Word);
        tray.IsActive.ShouldBeTrue();
        _world.Log.ShouldBeEmpty("a request denied for priority tries nothing");
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public async Task After_the_tray_menu_ends_other_leases_are_granted_again()
    {
        var tray = await _world.GrantAsync(LeaseKind.TrayMenu, TrayHost, LeaseOrigin.Tray);

        (await tray.RestoreAsync(TestContext.Current.CancellationToken)).ShouldBe(
            RestoreOutcome.Restored
        );
        var controlCenter = await _world.GrantAsync(
            LeaseKind.ControlCenter,
            ControlCenter,
            LeaseOrigin.Tray
        );

        _world.Control.Foreground.ShouldBe(ControlCenter);
        controlCenter.PreviousForeground.ShouldBe(Word);
    }

    [Fact]
    public async Task A_denied_request_leaves_the_active_lease_untouched()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Control.Script.Enqueue(false);

        var reason = await _world.DenyAsync(LeaseKind.TryNowTarget, Notepad, LeaseOrigin.Internal);

        reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        search.IsActive.ShouldBeTrue();
        _world.Orchestrator.ActiveLease.ShouldBeSameAs(search);
        _world.Orchestrator.IsActivationLeased(Search).ShouldBeTrue();
        _world.Surfaces.Activatable.ShouldBe([SearchSurface]);
    }

    [Fact]
    [Trait("Req", "BUS-001")]
    public async Task Switching_apps_ends_the_lease_without_restoring()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Mark();

        _world.Monitor.SwitchTo(Chrome);

        (await search.Ended).ShouldBe(LeaseEndReason.ForegroundChanged);
        _world.Orchestrator.ActiveLease.ShouldBeNull();
        _world.Orchestrator.IsActivationLeased(Search).ShouldBeFalse();
        _world.Log.ShouldBe(["external Chrome", "noactivate " + SearchSurface]);
        var outcome = await search.RestoreAsync(TestContext.Current.CancellationToken);
        outcome.ShouldBe(RestoreOutcome.Failed, "nothing may be sent to the app the user left");
        _world.Control.Foreground.ShouldBe(Chrome);
    }

    [Fact]
    public async Task A_late_report_of_the_previous_app_does_not_end_the_lease()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        var epoch = _world.Orchestrator.Current.Epoch;

        // The report of Word coming back after the previous lease arrives only now, with the search already in front.
        _world.Monitor.SwitchTo(Word, alsoForeground: false);

        search.IsActive.ShouldBeTrue();
        _world.Orchestrator.Current.Epoch.ShouldBe(epoch.Next());
        _world.Orchestrator.IsActivationLeased(Search).ShouldBeTrue();
    }

    [Fact]
    public async Task Going_back_to_the_previous_app_counts_as_restored()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);

        _world.Monitor.SwitchTo(Word);
        _ = await search.Ended;

        (await search.RestoreAsync(TestContext.Current.CancellationToken)).ShouldBe(
            RestoreOutcome.Restored
        );
        _world.Control.Attempts.ShouldBe(
            [Search],
            "verified with GetForegroundWindow, never retaken"
        );
    }

    [Fact]
    [Trait("Req", "PRB-004")]
    public async Task The_try_now_target_coming_to_the_front_does_not_end_its_own_lease()
    {
        var controlCenter = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);

        var tryNow = await _world.GrantAsync(LeaseKind.TryNowTarget, Notepad, LeaseOrigin.Touch);
        _world.Monitor.SwitchTo(Notepad);

        tryNow.IsActive.ShouldBeTrue();
        tryNow.PreviousForeground.ShouldBe(
            ControlCenter,
            "«Try now» returns to the Control Center"
        );
        controlCenter.EndReason.ShouldBe(LeaseEndReason.Replaced);
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    [Trait("Req", "PRB-004")]
    public async Task After_try_now_the_Control_Center_still_returns_to_the_app_before_it()
    {
        _ = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);
        _ = await _world.GrantAsync(LeaseKind.TryNowTarget, Notepad, LeaseOrigin.Touch);
        _world.Monitor.SwitchTo(Notepad);

        var back = await _world.GrantAsync(
            LeaseKind.ControlCenter,
            ControlCenter,
            LeaseOrigin.Internal
        );

        back.PreviousForeground.ShouldBe(Word);
    }

    [Fact]
    [Trait("Req", "BUS-002")]
    public async Task Text_input_ends_after_its_idle_timeout_and_gives_the_foreground_back()
    {
        var deadline = _world.Time.After(Timings.Foreground.TextInputLeaseIdle);
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);

        _world.Time.AdvanceToJustBefore(deadline);
        search.IsActive.ShouldBeTrue();
        _world.Time.AdvanceTo(deadline);

        (await search.Ended).ShouldBe(LeaseEndReason.IdleTimeout);
        _world.Control.Foreground.ShouldBe(Word);
        _world.Surfaces.Activatable.ShouldBeEmpty();
        (await search.RestoreAsync(TestContext.Current.CancellationToken)).ShouldBe(
            RestoreOutcome.Restored
        );
    }

    [Fact]
    [Trait("Req", "BUS-002")]
    public async Task Interaction_postpones_the_idle_timeout()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        var half = Timings.Foreground.TextInputLeaseIdle / 2;

        _world.Time.Advance(half);
        search.KeepAlive();
        _world.Time.Advance(half);
        search.IsActive.ShouldBeTrue("the idle time restarted at the interaction");
        _world.Time.Advance(half);

        (await search.Ended).ShouldBe(LeaseEndReason.IdleTimeout);
    }

    [Fact]
    public async Task Only_text_input_has_a_default_idle_timeout()
    {
        var controlCenter = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);

        _world.Time.Advance(Timings.Foreground.TextInputLeaseIdle * 4);

        controlCenter.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task An_explicit_idle_timeout_applies_to_any_kind()
    {
        var idle = Timings.Foreground.TextInputLeaseIdle * 2;
        var navigation = await _world.GrantAsync(
            LeaseKind.KeyboardNavigation,
            Panel,
            LeaseOrigin.GlobalHotkey,
            idle
        );

        _world.Time.Advance(idle);

        (await navigation.Ended).ShouldBe(LeaseEndReason.IdleTimeout);
        _world.Control.Foreground.ShouldBe(Word);
    }

    [Fact]
    public async Task A_terminal_event_ends_the_lease_and_restores()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);

        var outcome = await _world.Orchestrator.EndActiveLeaseAsync(
            TestContext.Current.CancellationToken
        );

        outcome.ShouldBe(RestoreOutcome.Restored);
        search.EndReason.ShouldBe(LeaseEndReason.Terminal);
        _world.Control.Foreground.ShouldBe(Word);
        (
            await _world.Orchestrator.EndActiveLeaseAsync(TestContext.Current.CancellationToken)
        ).ShouldBeNull();
    }

    [Fact]
    public async Task Every_external_change_moves_the_epoch()
    {
        var before = _world.Orchestrator.Current;

        _world.Monitor.SwitchTo(Chrome);
        _world.Monitor.SwitchTo(Word);

        before.Window.ShouldBe(Word);
        before.Epoch.Value.ShouldBe(1UL, "the first external foreground is epoch 1");
        _world.Orchestrator.Current.Window.ShouldBe(Word);
        _world.Orchestrator.Current.Epoch.Value.ShouldBe(3UL);
        _world.Orchestrator.Current.VerifiedAt.ShouldBe(_world.Time.GetUtcNow());
    }

    [Fact]
    public void Before_any_external_foreground_the_snapshot_is_empty()
    {
        using var world = new ForegroundWorld(seedWord: false);

        world.Orchestrator.Current.ShouldBe(ForegroundSnapshot.Empty);
    }

    [Fact]
    public void Disposing_stops_following_the_monitor()
    {
        _ = _world.Orchestrator;
        _world.Monitor.Subscribers.ShouldBe(1);

        _world.Orchestrator.Dispose();

        _world.Monitor.Subscribers.ShouldBe(0);
    }
}
