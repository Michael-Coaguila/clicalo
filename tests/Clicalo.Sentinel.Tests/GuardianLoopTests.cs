using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The guardian (blueprint §3.1, ADR-0004, S9): when the main process dies it releases exactly what the ledger records,
/// then relaunches according to the marks and the crash loop. Tested over a fake machine: nothing is injected.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "REG-03")]
public sealed class GuardianLoopTests
{
    private static readonly PhysicalKey Ctrl = new(0xA2, 0x1D, LedgerKeyAttributes.None);
    private static readonly PhysicalKey Shift = new(0xA0, 0x2A, LedgerKeyAttributes.None);

    private static readonly SentinelStartInfo Info = new(
        1,
        2,
        3,
        TimeSpan.FromSeconds(1),
        3,
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(30)
    );

    private static (KeyLedgerSection Engine, KeyLedgerSection View) HoldingCtrlShift()
    {
        var engine = KeyLedgerSection.CreateInMemory();
        foreach (var key in new[] { Ctrl, Shift })
        {
            engine.TryBeginDown(key, out var slot);
            engine.CommitDown(slot);
        }

        engine.SetMarks(LedgerMarks.EngineAlive);
        return (engine, engine.ReadOnlyView());
    }

    private static GuardianLoop Loop(
        KeyLedgerSection view,
        ILowLevelSender sender,
        IGuardianEnvironment environment
    ) =>
        new(
            Info,
            view,
            sender,
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero)),
            environment
        );

    [Fact]
    public void When_the_main_process_dies_holding_ctrl_shift_everything_goes_up_and_it_is_relaunched()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var sender = new RecordingSender();
            var environment = new FakeGuardianEnvironment();

            var exit = Loop(view, sender, environment).Run();

            exit.ShouldBe(SentinelExitCode.ReleasedAndRelaunched);
            sender
                .Batches.ShouldHaveSingleItem()
                .ShouldBe([LowLevelInput.KeyUp(Shift), LowLevelInput.KeyUp(Ctrl)]);
            environment
                .Relaunches.ShouldHaveSingleItem()
                .Single()
                .ShouldStartWith(CrashJournal.AfterCrashArgument);
        }
    }

    [Fact]
    public void A_clean_exit_with_nothing_recorded_does_nothing()
    {
        using var engine = KeyLedgerSection.CreateInMemory();
        engine.SetMarks(LedgerMarks.CleanShutdown);
        using var view = engine.ReadOnlyView();
        var sender = new RecordingSender();
        var environment = new FakeGuardianEnvironment();

        Loop(view, sender, environment).Run().ShouldBe(SentinelExitCode.CleanExit);

        sender.Batches.ShouldBeEmpty();
        environment.Relaunches.ShouldBeEmpty();
    }

    [Fact]
    public void An_update_or_a_handover_releases_without_relaunching()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            engine.SetMarks(LedgerMarks.CleanShutdown | LedgerMarks.NoRelaunch);
            var sender = new RecordingSender();
            var environment = new FakeGuardianEnvironment();

            Loop(view, sender, environment)
                .Run()
                .ShouldBe(SentinelExitCode.ReleasedWithoutRelaunch);

            sender.Sent.Count().ShouldBe(2);
            environment.Relaunches.ShouldBeEmpty();
        }
    }

    [Fact]
    public void A_broken_pipe_with_the_parent_alive_leaves_quietly()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var sender = new RecordingSender();
            var environment = new FakeGuardianEnvironment
            {
                Wake = GuardianWake.PipeBroken,
                ParentExitsAfterBrokenPipe = false,
            };

            Loop(view, sender, environment).Run().ShouldBe(SentinelExitCode.CleanExit);

            sender.Batches.ShouldBeEmpty();
            environment.Relaunches.ShouldBeEmpty();
        }
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    public void The_third_crash_in_the_window_relaunches_into_safe_mode()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var now = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
            var environment = new FakeGuardianEnvironment
            {
                Crashes = [now.AddMinutes(-5), now.AddMinutes(-2)],
            };

            Loop(view, new RecordingSender(), environment)
                .Run()
                .ShouldBe(SentinelExitCode.ReleasedAndRelaunched);

            environment
                .Relaunches.Single()
                .Contains(CrashJournal.SafeModeArgument, StringComparer.Ordinal)
                .ShouldBeTrue();
        }
    }

    [Fact]
    public void A_relaunch_that_fails_still_released()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var sender = new RecordingSender();

            Loop(view, sender, new FakeGuardianEnvironment { CanRelaunch = false })
                .Run()
                .ShouldBe(SentinelExitCode.ReleasedWithoutRelaunch);

            sender.Sent.Count().ShouldBe(2);
        }
    }

    [Fact]
    public void Bad_arguments_end_sentinel_with_invalid_arguments() =>
        SentinelEntryPoint.Run(["--protocol=9"]).ShouldBe((int)SentinelExitCode.InvalidArguments);

    [Fact]
    public void Arguments_of_protocol_1_end_sentinel_with_invalid_arguments()
    {
        var protocol1 = Info.ToArguments().SetItem(0, "--protocol=1").RemoveAt(6);

        SentinelEntryPoint.Run(protocol1.AsSpan()).ShouldBe((int)SentinelExitCode.InvalidArguments);
    }

    [Fact]
    public void A_handle_that_is_not_a_ledger_ends_sentinel_with_ledger_unreadable()
    {
        var arguments = new SentinelStartInfo(
            1,
            0x7FFF_FFF0,
            3,
            TimeSpan.FromSeconds(1),
            3,
            TimeSpan.FromMinutes(10),
            TimeSpan.FromSeconds(30)
        ).ToArguments();

        SentinelEntryPoint.Run(arguments.AsSpan()).ShouldBe((int)SentinelExitCode.LedgerUnreadable);
    }
}
