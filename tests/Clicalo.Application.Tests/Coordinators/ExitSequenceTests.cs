using Clicalo.Application.Coordinators;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>
/// How Clícalo ends (blueprint §7.6, SEG-006): the engine releases everything and stops, then the document is flushed;
/// a step that does not finish in time never keeps the process alive, because Sentinel releases whatever Windows still
/// reports down (ADR-0023).
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-007")]
public sealed class ExitSequenceTests
{
    private readonly List<string> _log = [];
    private readonly RecordingEngineInbox _engine = new();
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly ExitSequence _exit;

    public ExitSequenceTests()
    {
        _engine.OnPost = engineEvent => _log.Add("post " + engineEvent.GetType().Name);
        _exit = new ExitSequence(_engine, _time);
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public async Task The_engine_releases_first_and_the_flush_comes_last()
    {
        var report = await _exit.RunAsync(
            TerminalReason.Exit,
            _ =>
            {
                _log.Add("engine stopped");
                return Task.CompletedTask;
            },
            _ =>
            {
                _log.Add("flush");
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken
        );

        _log.ShouldBe(["post Terminal", "engine stopped", "flush"]);
        var terminal = _engine.Events.ShouldHaveSingleItem().ShouldBeOfType<EngineEvent.Terminal>();
        terminal.Reason.ShouldBe(TerminalReason.Exit);
        terminal.Lane.ShouldBe(EngineLane.Priority);
        report.EngineReleased.ShouldBeTrue();
        report.Flushed.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task An_engine_that_does_not_stop_in_time_does_not_prevent_the_flush()
    {
        var never = new TaskCompletionSource();
        var flushed = false;

        var running = _exit.RunAsync(
            TerminalReason.SessionEnd,
            _ => never.Task,
            _ =>
            {
                flushed = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken
        );
        _time.AdvanceToJustBefore(_time.After(Timings.App.ExitReleaseWait));
        running.IsCompleted.ShouldBeFalse("the engine still has time");
        _time.Advance(TestTime.Tick);
        var report = await running;

        report.EngineReleased.ShouldBeFalse();
        flushed.ShouldBeTrue();
        report.Flushed.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "DAT-002")]
    public async Task A_flush_that_hangs_ends_after_its_limit()
    {
        var hanging = new TaskCompletionSource();

        var running = _exit.RunAsync(
            TerminalReason.Exit,
            _ => Task.CompletedTask,
            _ => hanging.Task,
            TestContext.Current.CancellationToken
        );
        _time.Advance(Timings.App.ExitFlushTimeout - TestTime.Tick);
        running.IsCompleted.ShouldBeFalse();
        _time.Advance(TestTime.Tick);
        var report = await running;

        report.Flushed.ShouldBeFalse();
        report.EngineReleased.ShouldBeTrue();
        report.Elapsed.ShouldBe(Timings.App.ExitFlushTimeout);
    }

    [Fact]
    public async Task A_failing_step_is_reported_and_the_next_one_still_runs()
    {
        var flushed = false;

        var report = await _exit.RunAsync(
            TerminalReason.Exit,
            _ => throw new InvalidOperationException("engine thread died"),
            _ =>
            {
                flushed = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken
        );

        report.EngineReleased.ShouldBeFalse();
        flushed.ShouldBeTrue();
    }

    [Fact]
    public async Task An_engine_that_already_stopped_is_not_reported_as_released_by_this_exit()
    {
        _engine.Stop();

        var report = await _exit.RunAsync(
            TerminalReason.Exit,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        report.EngineReleased.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TerminalReason.Hide)]
    [InlineData(TerminalReason.Lock)]
    [InlineData(TerminalReason.Relaunch)]
    public async Task Only_an_exit_or_the_end_of_the_session_ends_the_process(
        TerminalReason reason
    ) =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            _exit.RunAsync(
                reason,
                _ => Task.CompletedTask,
                _ => Task.CompletedTask,
                TestContext.Current.CancellationToken
            )
        );
}
