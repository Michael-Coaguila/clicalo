using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Application.Tests.Coordinators;
using Clicalo.Application.Tests.Engine;
using Clicalo.Application.Tests.Foreground;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.UseCases.Editor;

/// <summary>«Probar ahora» cancelled halfway (PRB-005): no key stays down (REG-03) and the Control Center comes back.</summary>
[Trait("Req", "PRB-004")]
[Trait("Req", "PRB-005")]
[Trait("Req", "REG-03")]
public sealed class TryNowRunTests
{
    private static readonly OpenApp Notepad = new(
        new ProcessName("notepad.exe"),
        "Bloc de notas",
        ForegroundWorld.Notepad,
        Elevated: false,
        ExecutablePath: null
    );

    [Fact]
    public async Task Cancelling_while_a_hold_is_down_releases_it_and_shows_the_control_center_again()
    {
        using var world = new ForegroundWorld();
        var time = new FakeTimeProvider(TestTime.Epoch);
        var inbox = new RecordingEngineInbox();
        var run = new TryNowRun(
            new GrantingForeground(world.Orchestrator),
            inbox,
            () => 0,
            time,
            selfElevated: false
        );
        var window = new ControlCenterWindow();
        using var cancel = new CancellationTokenSource();
        var hold = TestTiles.Shortcut(
            "ptt",
            new HoldAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C))
        );

        var pending = run.RunAsync(
                hold,
                null,
                InjectionMode.ScanCode,
                Notepad,
                window,
                cancel.Token
            )
            .AsTask();
        time.Advance(Timings.TryNow.TryNowSendDelay);
        await Eventually.WaitUntilAsync(() => inbox.Events.Count == 1, "the hold was pressed");
        await cancel.CancelAsync();
        var (outcome, lease) = await pending.WaitAsync(
            Eventually.Liveness,
            TestContext.Current.CancellationToken
        );

        outcome.ShouldBe(TryNowOutcome.Cancelled);
        lease.ShouldNotBeNull();
        inbox.Events[0].ShouldBeOfType<EngineEvent.Activation>();
        inbox.Events[1].ShouldBe(new EngineEvent.ReleaseAll(ReleaseReason.User));
        inbox.Events.Count.ShouldBe(2);
        window.Hidden.ShouldBe(1);
        window.Shown.ShouldBe(1);
    }

    [Fact]
    public async Task A_shortcut_that_asks_for_confirmation_is_sent_already_confirmed()
    {
        using var world = new ForegroundWorld();
        var time = new FakeTimeProvider(TestTime.Epoch);
        var inbox = new RecordingEngineInbox();
        var run = new TryNowRun(
            new GrantingForeground(world.Orchestrator),
            inbox,
            () => 0,
            time,
            selfElevated: false
        );
        var window = new ControlCenterWindow();
        var tap = TestTiles.Shortcut(
            "copy",
            new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C), [])
        );
        var asksFirst = tap with { Options = tap.Options with { Confirm = true } };

        var pending = run.RunAsync(
                asksFirst,
                null,
                InjectionMode.VirtualKey,
                Notepad,
                window,
                TestContext.Current.CancellationToken
            )
            .AsTask();
        time.Advance(Timings.TryNow.TryNowSendDelay);
        await Eventually.WaitUntilAsync(() => inbox.Events.Count == 1, "the tap was sent");
        time.Advance(Timings.TryNow.TryNowReturnDelay);
        var (outcome, _) = await pending.WaitAsync(
            Eventually.Liveness,
            TestContext.Current.CancellationToken
        );

        // PRB-004: the person confirmed it in the Control Center, so the engine runs it instead of arming it.
        outcome.ShouldBe(TryNowOutcome.Asked);
        inbox
            .Events[0]
            .ShouldBeOfType<EngineEvent.Activation>()
            .Shortcut.Options.Confirm.ShouldBeFalse();
        inbox.Events.Count.ShouldBe(1);
    }

    private sealed class ControlCenterWindow : ITryNowWindow
    {
        public WindowToken Window => ForegroundWorld.ControlCenter;

        public int Hidden { get; private set; }

        public int Shown { get; private set; }

        public void Hide() => Hidden++;

        public void Show() => Shown++;
    }

    /// <summary>Grants every lease at once; the leases are never ended in these tests.</summary>
    private sealed class GrantingForeground(ForegroundOrchestrator owner) : IForegroundOrchestrator
    {
        public ForegroundSnapshot Current => ForegroundSnapshot.Empty;

        public ValueTask<LeaseResult> AcquireAsync(
            LeaseRequest request,
            CancellationToken cancellationToken
        ) =>
            ValueTask.FromResult<LeaseResult>(
                new LeaseResult.Granted(
                    new ForegroundLease(
                        owner,
                        request,
                        WindowToken.None,
                        WindowToken.None,
                        default,
                        LadderStep.Direct,
                        null,
                        null
                    )
                )
            );
    }
}
