using Clicalo.Application.Foreground;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Time;
using static Clicalo.Application.Tests.Foreground.ForegroundWorld;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// The foreground rights ladder per origin (blueprint §3.6, spike S4): Touch, Tray and GlobalHotkey use step 1 with
/// one retry; UiaInvoke climbs to the internal rights hotkey (armed before it is sent, never sent unregistered);
/// Internal uses step 1 only; everything else ends in <see cref="LeaseResult.Denied"/> with its reason.
/// </summary>
[Trait("Req", "BUS-002")]
[Trait("Req", "REG-01")]
public sealed class ForegroundOrchestratorLadderTests : IDisposable
{
    private readonly ForegroundWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Theory]
    [InlineData(LeaseOrigin.Touch)]
    [InlineData(LeaseOrigin.Tray)]
    [InlineData(LeaseOrigin.GlobalHotkey)]
    [InlineData(LeaseOrigin.UiaInvoke)]
    [InlineData(LeaseOrigin.Internal)]
    public async Task Step_1_grants_the_lease_when_Clicalo_holds_the_right(LeaseOrigin origin)
    {
        _world.Control.HasRights = true;

        var lease = await _world.GrantAsync(LeaseKind.TextInput, Search, origin);

        lease.GrantedAt.ShouldBe(LadderStep.Direct);
        lease.Origin.ShouldBe(origin);
        _world.Control.Attempts.ShouldBe([Search]);
        _world.Control.Foreground.ShouldBe(Search);
        _world.Hotkey.Armed.ShouldBe(0);
        _world.Keys.RightsChords.ShouldBe(0);
    }

    [Theory]
    [InlineData(LeaseOrigin.Touch)]
    [InlineData(LeaseOrigin.Tray)]
    [InlineData(LeaseOrigin.GlobalHotkey)]
    public async Task Input_origins_retry_step_1_once_after_the_retry_delay(LeaseOrigin origin)
    {
        _world.Control.Script.Enqueue(false);
        _world.Control.Script.Enqueue(true);
        var deadline = _world.Time.After(Timings.Foreground.RestoreRetryDelay);

        var pending = _world.Orchestrator.AcquireAsync(
            Request(LeaseKind.TextInput, Search, origin),
            TestContext.Current.CancellationToken
        );
        _world.Time.AdvanceToJustBefore(deadline);
        _world.Control.Attempts.Count.ShouldBe(1, "the retry waits for the retry delay");
        _world.Time.AdvanceTo(deadline);
        var result = await pending;

        result
            .ShouldBeOfType<LeaseResult.Granted>()
            .Lease.GrantedAt.ShouldBe(LadderStep.DirectRetry);
        _world.Control.Attempts.ShouldBe([Search, Search]);
        _world.Keys.RightsChords.ShouldBe(0, "only UI Automation origins climb to step 2");
    }

    [Theory]
    [InlineData(LeaseOrigin.Touch)]
    [InlineData(LeaseOrigin.Tray)]
    [InlineData(LeaseOrigin.GlobalHotkey)]
    public async Task Input_origins_are_denied_after_the_retry_and_nothing_changes(
        LeaseOrigin origin
    )
    {
        var pending = _world.Orchestrator.AcquireAsync(
            Request(LeaseKind.TextInput, Search, origin),
            TestContext.Current.CancellationToken
        );
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);
        var result = await pending;

        result
            .ShouldBeOfType<LeaseResult.Denied>()
            .Reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        _world.Control.Attempts.ShouldBe([Search, Search]);
        _world.Control.Foreground.ShouldBe(Word);
        _world.Surfaces.Activatable.ShouldBeEmpty("WS_EX_NOACTIVATE goes back after a denial");
        _world.Orchestrator.ActiveLease.ShouldBeNull();
        _world.Orchestrator.IsActivationLeased(Search).ShouldBeFalse();
        _world.Hotkey.Armed.ShouldBe(0);
    }

    [Fact]
    public async Task Uia_origin_arms_the_wait_before_sending_the_chord_and_retries_after_WM_HOTKEY()
    {
        _world.Mark();

        var lease = await _world.GrantAsync(LeaseKind.TextInput, Search, LeaseOrigin.UiaInvoke);

        lease.GrantedAt.ShouldBe(LadderStep.RightsHotkey);
        _world.Log.ShouldBe([
            "allow " + SearchSurface,
            "refused Search",
            "arm",
            "send rights chord",
            "hotkey",
            "set Search",
        ]);
        _world.Keys.RightsChords.ShouldBe(1);
    }

    [Fact]
    public async Task Uia_origin_sends_nothing_when_the_chord_is_not_registered()
    {
        _world.Hotkey.IsRegistered = false;

        var reason = await _world.DenyAsync(LeaseKind.TextInput, Search, LeaseOrigin.UiaInvoke);

        reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        _world.Keys.RightsChords.ShouldBe(
            0,
            "an unregistered chord would reach the foreground app"
        );
        _world.Hotkey.Armed.ShouldBe(0);
        _world.Control.Attempts.ShouldBe([Search]);
        _world.Surfaces.Activatable.ShouldBeEmpty();
    }

    [Fact]
    public async Task Uia_origin_is_denied_when_WM_HOTKEY_never_arrives()
    {
        _world.Keys.OnRightsChord = () =>
        {
            _world.Hotkey.TimeOut();
            return true;
        };

        var reason = await _world.DenyAsync(LeaseKind.TextInput, Search, LeaseOrigin.UiaInvoke);

        reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        _world.Control.Attempts.ShouldBe(
            [Search],
            "without WM_HOTKEY there is nothing to retry with"
        );
        _world.Control.Foreground.ShouldBe(Word);
    }

    [Fact]
    public async Task Uia_origin_disarms_the_wait_when_the_chord_cannot_be_sent()
    {
        _world.Keys.OnRightsChord = () => false;

        var reason = await _world.DenyAsync(LeaseKind.TextInput, Search, LeaseOrigin.UiaInvoke);

        reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        _world.Hotkey.IsArmed.ShouldBeFalse();
        _world.Log.ShouldContain(entry => string.Equals(entry, "disarm", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Uia_origin_is_denied_when_the_rights_arrive_but_Windows_still_refuses()
    {
        _world.Keys.OnRightsChord = () =>
        {
            _world.Hotkey.Arrive();
            return true;
        };

        var reason = await _world.DenyAsync(LeaseKind.TextInput, Search, LeaseOrigin.UiaInvoke);

        reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        _world.Control.Attempts.ShouldBe([Search, Search]);
    }

    [Fact]
    public async Task Uia_origin_is_denied_when_the_user_switches_apps_while_waiting_for_the_hotkey()
    {
        _world.Keys.OnRightsChord = () =>
        {
            _world.Monitor.SwitchTo(Chrome);
            return _world.Keys.DeliverRights();
        };

        var reason = await _world.DenyAsync(LeaseKind.TextInput, Search, LeaseOrigin.UiaInvoke);

        reason.ShouldBe(ForegroundDenialReason.ForegroundChanged);
        _world.Control.Attempts.ShouldBe(
            [Search],
            "the foreground now belongs to the app the user chose"
        );
        _world.Control.Foreground.ShouldBe(Chrome);
    }

    [Fact]
    public async Task Internal_origin_uses_step_1_only()
    {
        var reason = await _world.DenyAsync(LeaseKind.TryNowTarget, Notepad, LeaseOrigin.Internal);

        reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        _world.Control.Attempts.ShouldBe([Notepad]);
        _world.Control.Flashed.ShouldBeEmpty();
        _world.Keys.RightsChords.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "PRB-007")]
    public async Task Internal_origin_flashes_the_Control_Center_when_it_cannot_come_back()
    {
        var reason = await _world.DenyAsync(
            LeaseKind.ControlCenter,
            ControlCenter,
            LeaseOrigin.Internal
        );

        reason.ShouldBe(ForegroundDenialReason.RightsRefused);
        _world.Control.Flashed.ShouldBe([ControlCenter]);
    }

    [Fact]
    public async Task A_request_without_a_target_is_denied_before_any_attempt()
    {
        var reason = await _world.DenyAsync(
            LeaseKind.ControlCenter,
            Clicalo.Application.Ports.WindowToken.None,
            LeaseOrigin.Touch
        );

        reason.ShouldBe(ForegroundDenialReason.TargetUnavailable);
        _world.Control.Attempts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancelling_during_the_retry_delay_leaves_nothing_leased()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        var pending = _world
            .Orchestrator.AcquireAsync(
                Request(LeaseKind.TextInput, Search, LeaseOrigin.Touch),
                cancel.Token
            )
            .AsTask();

        await cancel.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(pending);
        _world.Orchestrator.ActiveLease.ShouldBeNull();
        _world.Orchestrator.IsActivationLeased(Search).ShouldBeFalse();
        _world.Surfaces.Activatable.ShouldBeEmpty();
    }
}
