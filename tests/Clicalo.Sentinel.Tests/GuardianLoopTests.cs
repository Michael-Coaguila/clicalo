using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The guardian loop (ADR-0022) with a fake key state and a fake <c>SendInput</c>: when the main process ends it
/// releases exactly what is down, tries again every retry interval while Windows refuses (decision D3: release before
/// relaunching), and relaunches only after an abnormal exit, into safe mode at the crash loop.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-007")]
[Trait("Req", "REG-03")]
public sealed class GuardianLoopTests
{
    private const byte A = 0x41;
    private const byte LeftCtrl = 0xA2;
    private const byte LeftAlt = 0xA4;
    private const byte LeftWin = 0x5B;

    private static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    private static readonly SentinelStartInfo Info = new(
        0x1A4,
        TimeSpan.FromSeconds(1),
        3,
        TimeSpan.FromMinutes(10)
    );

    private readonly FakeKeyState _state = new();
    private readonly FakeGuardianEnvironment _environment = new();
    private readonly List<string> _log = [];
    private readonly FakeSender _sender;

    public GuardianLoopTests()
    {
        _sender = new FakeSender(_state) { Log = _log };
        _environment.Log = _log;
    }

    [Fact]
    public void After_a_crash_it_releases_exactly_what_is_down_and_then_relaunches()
    {
        _state.Down.UnionWith([A, LeftCtrl, 0x01]);

        var exit = Loop().Run();

        exit.ShouldBe(SentinelExitCode.ReleasedAndRelaunched);
        _state.Down.ShouldBeEmpty();
        _sender.Batches.ShouldHaveSingleItem();
        _sender
            .Batches[0]
            .Where(static input => input.Kind == LowLevelInputKind.KeyUp)
            .Select(static input => (byte)input.Key.Vk)
            .ShouldBe([A, LeftCtrl]);
        _log.ShouldBe(["send", "relaunch"]);
        _environment
            .Relaunches.ShouldHaveSingleItem()
            .ShouldBe(CrashJournal.RelaunchArguments(Now, safeMode: false));
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    public void After_an_exit_code_of_zero_it_releases_but_never_relaunches()
    {
        _environment.ParentExitCode = 0;
        _state.Down.Add(A);

        var exit = Loop().Run();

        exit.ShouldBe(SentinelExitCode.CleanExit);
        _state.Down.ShouldBeEmpty();
        _environment.Relaunches.ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_down_sends_nothing_and_still_relaunches_after_a_crash()
    {
        Loop().Run().ShouldBe(SentinelExitCode.ReleasedAndRelaunched);

        _sender.Batches.ShouldBeEmpty();
        _environment.Waits.ShouldBeEmpty();
    }

    [Fact]
    public void While_windows_refuses_it_tries_again_every_interval_and_relaunches_only_after_the_release()
    {
        _state.Down.Add(LeftCtrl);
        _sender.RefusalsLeft = 3;
        var loop = Loop();

        loop.Run();

        loop.Attempts.ShouldBe(4);
        _environment.Waits.ShouldBe([
            Info.ReleaseRetryInterval,
            Info.ReleaseRetryInterval,
            Info.ReleaseRetryInterval,
        ]);
        _state.Down.ShouldBeEmpty();
        _log.ShouldBe(["send", "send", "send", "send", "relaunch"]);
    }

    [Fact]
    public void While_the_session_is_locked_it_waits_without_sending_until_it_can_read_the_keys()
    {
        _state.Down.Add(A);
        _state.CanRead = false;
        _environment.OnWait = attempt => _state.CanRead = attempt >= 5;

        Loop().Run();

        _environment.Waits.Count.ShouldBe(5);
        _log.ShouldBe(["send", "relaunch"]);
        _state.Down.ShouldBeEmpty();
    }

    [Fact]
    public void A_release_taken_only_in_part_is_completed_by_the_next_attempt_with_the_mask_again()
    {
        _state.Down.UnionWith([LeftAlt, LeftWin]);

        // The first call stops right after Alt went up: Win stays down.
        _sender.TakeNext = 3;

        Loop().Run();

        _state.Down.ShouldBeEmpty();
        _sender.Batches.Count.ShouldBe(2);
        _sender
            .Batches[1]
            .ShouldBe([
                LowLevelInput.KeyDown(PressedInputRelease.MenuMask),
                LowLevelInput.KeyUp(PressedInputRelease.MenuMask),
                LowLevelInput.KeyUp(PressedInputRelease.KeyOf(_state, LeftWin)),
            ]);
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    public void The_third_crash_in_the_window_relaunches_into_safe_mode()
    {
        _environment.Crashes = [Now.AddMinutes(-9), Now.AddMinutes(-1)];

        Loop().Run();

        _environment
            .Relaunches.ShouldHaveSingleItem()
            .ShouldBe(CrashJournal.RelaunchArguments(Now, safeMode: true));
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    public void Past_the_crash_loop_it_releases_and_stops_relaunching()
    {
        _environment.Crashes = [Now.AddMinutes(-9), Now.AddMinutes(-5), Now.AddMinutes(-1)];
        _state.Down.Add(A);

        Loop().Run().ShouldBe(SentinelExitCode.ReleasedWithoutRelaunch);

        _state.Down.ShouldBeEmpty();
        _environment.Relaunches.ShouldBeEmpty();
    }

    [Fact]
    public void A_relaunch_that_fails_is_reported()
    {
        _environment.CanRelaunch = false;

        Loop().Run().ShouldBe(SentinelExitCode.ReleasedWithoutRelaunch);
    }

    [Fact]
    public void A_parent_it_cannot_wait_on_releases_nothing_and_relaunches_nothing()
    {
        _environment.ParentExitCode = null;
        _state.Down.Add(A);

        Loop().Run().ShouldBe(SentinelExitCode.InvalidArguments);

        _state.Down.ShouldBe([A]);
        _log.ShouldBeEmpty();
    }

    private GuardianLoop Loop() =>
        new(Info, _state, _sender, new FakeTimeProvider(Now), _environment);
}
