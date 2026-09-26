using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Time;
using static Clicalo.Application.Tests.Foreground.ForegroundWorld;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// Verified restoration (blueprint §3.6): <c>GetForegroundWindow() == prev</c> with one retry after
/// <c>Timings.Foreground.RestoreRetryDelay</c> → <see cref="RestoreOutcome.Restored"/>,
/// <see cref="RestoreOutcome.RestoredAfterRetry"/> or <see cref="RestoreOutcome.Failed"/>; «Try now» flashes the
/// Control Center instead (PRB-007); <c>AllowActivation</c> when granted and <c>RestoreNoActivate</c> always at the
/// end, also when restoring fails.
/// </summary>
[Trait("Req", "BUS-002")]
[Trait("Req", "REG-01")]
public sealed class ForegroundOrchestratorRestoreTests : IDisposable
{
    private readonly ForegroundWorld _world = new();

    public ForegroundOrchestratorRestoreTests() => _world.Control.HasRights = true;

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task A_text_lease_allows_activation_before_the_attempt_and_removes_it_after_restoring()
    {
        _world.Mark();
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);

        var outcome = await search.RestoreAsync(TestContext.Current.CancellationToken);

        outcome.ShouldBe(RestoreOutcome.Restored);
        _world.Log.ShouldBe([
            "allow " + SearchSurface,
            "set Search",
            "set Word",
            "noactivate " + SearchSurface,
        ]);
        search.EndReason.ShouldBe(LeaseEndReason.Released);
        _world.Orchestrator.ActiveLease.ShouldBeNull();
    }

    [Fact]
    public async Task The_restoration_is_retried_once_after_the_retry_delay()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Control.Script.Enqueue(false);
        _world.Control.Script.Enqueue(true);
        var deadline = _world.Time.After(Timings.Foreground.RestoreRetryDelay);

        var pending = await _world.StartUntilItWaitsAsync(() =>
            search.RestoreAsync(TestContext.Current.CancellationToken).AsTask()
        );
        _world.Time.AdvanceToJustBefore(deadline);
        search.IsActive.ShouldBeTrue("the lease ends after the retry");
        _world.Time.AdvanceTo(deadline);

        (await pending).ShouldBe(RestoreOutcome.RestoredAfterRetry);
        _world.Control.Foreground.ShouldBe(Word);
    }

    [Fact]
    public async Task A_first_attempt_that_Windows_completes_during_the_verification_delay_counts_as_restored()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Control.Script.Enqueue(false);

        var pending = await _world.StartUntilItWaitsAsync(() =>
            search.RestoreAsync(TestContext.Current.CancellationToken).AsTask()
        );
        _world.Control.Foreground = Word;
        _world.Time.Advance(Timings.Foreground.RestoreRetryDelay);

        (await pending).ShouldBe(
            RestoreOutcome.Restored,
            "the first attempt, verified once Windows finished"
        );
        _world.Control.Attempts.ShouldBe([Search, Word], "verified, not set a second time");
    }

    [Fact]
    public async Task When_both_attempts_fail_the_outcome_is_Failed_and_the_surface_is_non_activatable_again()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Control.Script.Enqueue(false);
        _world.Control.Script.Enqueue(false);

        var pending = search.RestoreAsync(TestContext.Current.CancellationToken);

        (await _world.CompleteAsync(pending.AsTask())).ShouldBe(RestoreOutcome.Failed);
        _world.Surfaces.Activatable.ShouldBeEmpty();
        search.IsActive.ShouldBeFalse();
        _world.Control.Flashed.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "PRB-004")]
    [Trait("Req", "PRB-007")]
    public async Task Try_now_returns_to_the_Control_Center_or_flashes_it()
    {
        _ = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);
        var tryNow = await _world.GrantAsync(LeaseKind.TryNowTarget, Notepad);
        _world.Control.HasRights = false;

        var pending = tryNow.RestoreAsync(TestContext.Current.CancellationToken);

        (await _world.CompleteAsync(pending.AsTask())).ShouldBe(RestoreOutcome.Flashed);
        _world.Control.Flashed.ShouldBe([ControlCenter]);
        _world.Control.Foreground.ShouldBe(Notepad);
    }

    [Fact]
    [Trait("Req", "PRB-004")]
    public async Task Try_now_gives_the_foreground_back_to_the_Control_Center()
    {
        _ = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);
        var tryNow = await _world.GrantAsync(LeaseKind.TryNowTarget, Notepad);

        (await tryNow.RestoreAsync(TestContext.Current.CancellationToken)).ShouldBe(
            RestoreOutcome.Restored
        );
        _world.Control.Foreground.ShouldBe(ControlCenter);
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    public async Task Closing_the_Control_Center_returns_to_the_app_that_was_in_front_before_it()
    {
        var controlCenter = await _world.GrantAsync(
            LeaseKind.ControlCenter,
            ControlCenter,
            LeaseOrigin.Tray
        );

        await controlCenter.DisposeAsync();

        controlCenter.EndReason.ShouldBe(LeaseEndReason.Released);
        _world.Control.Foreground.ShouldBe(Word);
    }

    [Fact]
    public async Task Restoring_is_idempotent_and_disposal_after_it_restores_nothing()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);

        var first = await search.RestoreAsync(TestContext.Current.CancellationToken);
        _world.Mark();
        var second = await search.RestoreAsync(TestContext.Current.CancellationToken);
        await search.DisposeAsync();

        second.ShouldBe(first);
        _world.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_a_previous_window_there_is_nothing_to_verify()
    {
        using var world = new ForegroundWorld(seedWord: false);
        world.Control.HasRights = true;
        var controlCenter = await world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);
        world.Mark();

        (await controlCenter.RestoreAsync(TestContext.Current.CancellationToken)).ShouldBe(
            RestoreOutcome.Failed
        );
        controlCenter.PreviousForeground.ShouldBe(WindowToken.None);
        world.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancelling_during_the_retry_still_ends_the_lease_and_puts_WS_EX_NOACTIVATE_back()
    {
        var search = await _world.GrantAsync(LeaseKind.TextInput, Search);
        _world.Control.Script.Enqueue(false);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );

        // Cancelled while it waits to verify the first attempt.
        var pending = await _world.StartUntilItWaitsAsync(() =>
            search.RestoreAsync(cancel.Token).AsTask()
        );
        await cancel.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(pending);
        search.IsActive.ShouldBeFalse();
        _world.Surfaces.Activatable.ShouldBeEmpty();
        _world.Orchestrator.IsActivationLeased(Search).ShouldBeFalse();
        (await search.RestoreAsync(TestContext.Current.CancellationToken)).ShouldBe(
            RestoreOutcome.Failed
        );
    }

    [Fact]
    public async Task Non_surface_targets_never_change_the_activation_style()
    {
        var controlCenter = await _world.GrantAsync(LeaseKind.ControlCenter, ControlCenter);
        _ = await controlCenter.RestoreAsync(TestContext.Current.CancellationToken);

        _world.Log.ShouldNotContain(entry => entry.StartsWith("allow", StringComparison.Ordinal));
        _world.Log.ShouldNotContain(entry =>
            entry.StartsWith("noactivate", StringComparison.Ordinal)
        );
    }
}
