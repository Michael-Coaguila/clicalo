using Clicalo.Tools.SpikeLab.Scripting;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Tools.SpikeLab.Tests.Scripting;

public sealed class ScriptEngineTests
{
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public void An_automatic_step_passes_after_twenty_clean_repetitions_and_the_final_check()
    {
        var engine = Engine(TestScripts.Tap("1"));

        for (var i = 0; i < 20; i++)
        {
            engine.RecordAutomatic(TestScripts.Clean).ShouldNotBeNull().Passed.ShouldBeTrue();
        }

        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.InProgress);
        engine.MarkWorked(RepetitionEvidence.Empty).ShouldBeNull();

        var step = engine.Snapshot().Steps[0];
        step.Verdict.ShouldBe(StepVerdict.Passed);
        step.PassedCount.ShouldBe(20);
        step.Confirmed.ShouldBeTrue();
    }

    [Fact]
    public void Nineteen_of_twenty_is_not_enough_even_when_confirmed()
    {
        var engine = Engine(TestScripts.Tap("1"));
        for (var i = 0; i < 19; i++)
        {
            engine.RecordAutomatic(TestScripts.Clean);
        }

        engine.MarkWorked(RepetitionEvidence.Empty);

        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.InProgress);
        engine.Next();
        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.Incomplete);
    }

    [Fact]
    public void A_repetition_whose_evidence_breaks_a_check_fails_the_step()
    {
        var engine = Engine(TestScripts.Tap("1"));

        var record = engine.RecordAutomatic(TestScripts.Activated).ShouldNotBeNull();

        record.Passed.ShouldBeFalse();
        record.Problems.ShouldContain(problem =>
            problem.Contains("WM_ACTIVATE", StringComparison.Ordinal)
        );
        record.Problems.ShouldContain(problem =>
            problem.Contains("ventana de SpikeLab", StringComparison.Ordinal)
        );
        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.Failed);
    }

    [Fact]
    public void Repetitions_are_numbered_and_stamped_with_the_time_provider()
    {
        var engine = Engine(TestScripts.Tap("1"));
        engine.RecordAutomatic(TestScripts.Clean);
        _time.Advance(TimeSpan.FromSeconds(3));

        var second = engine.RecordAutomatic(TestScripts.Clean).ShouldNotBeNull();

        second.Index.ShouldBe(2);
        second.At.ShouldBe(_time.GetUtcNow());
        second.Source.ShouldBe(RepetitionSource.Automatic);
    }

    [Fact]
    public void Falló_on_an_automatic_step_marks_the_last_repetition_and_withdraws_the_confirmation()
    {
        var engine = Engine(TestScripts.Tap("1"));
        for (var i = 0; i < 20; i++)
        {
            engine.RecordAutomatic(TestScripts.Clean);
        }

        engine.MarkWorked(RepetitionEvidence.Empty);

        var failed = engine.MarkFailed(RepetitionEvidence.Empty);

        failed.Index.ShouldBe(20);
        failed.FailedByUser.ShouldBeTrue();
        failed.Passed.ShouldBeFalse();
        var step = engine.Snapshot().Steps[0];
        step.Repetitions.Length.ShouldBe(20);
        step.Confirmed.ShouldBeFalse();
        step.Verdict.ShouldBe(StepVerdict.Failed);
    }

    [Fact]
    public void Falló_twice_after_the_same_repetition_adds_a_failed_one()
    {
        var engine = Engine(TestScripts.Tap("1"));
        engine.RecordAutomatic(TestScripts.Clean);

        engine.MarkFailed(RepetitionEvidence.Empty);
        engine.MarkFailed(RepetitionEvidence.Empty);

        engine.Snapshot().Steps[0].Repetitions.Length.ShouldBe(2);
        engine.Snapshot().Steps[0].FailedCount.ShouldBe(2);
    }

    [Fact]
    public void Falló_before_any_repetition_records_a_failed_one()
    {
        var engine = Engine(TestScripts.Tap("1"));

        var failed = engine.MarkFailed(RepetitionEvidence.Empty);

        failed.Index.ShouldBe(1);
        failed.Source.ShouldBe(RepetitionSource.User);
        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.Failed);
    }

    [Fact]
    public void A_manual_step_counts_each_mark_as_one_repetition()
    {
        var engine = Engine(TestScripts.Manual("9a"));

        engine
            .MarkWorked(RepetitionEvidence.Empty)
            .ShouldNotBeNull()
            .Source.ShouldBe(RepetitionSource.User);
        engine.MarkWorked(RepetitionEvidence.Empty);
        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.InProgress);
        engine.MarkWorked(RepetitionEvidence.Empty);

        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.Passed);
    }

    [Fact]
    public void Funcionó_on_a_manual_step_still_fails_when_the_evidence_breaks_a_check()
    {
        var engine = Engine(TestScripts.Manual("9a"));

        var record = engine.MarkWorked(TestScripts.Activated).ShouldNotBeNull();

        record.Passed.ShouldBeFalse();
        record.FailedByUser.ShouldBeFalse();
        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.Failed);
    }

    [Fact]
    public void Automatic_triggers_are_ignored_on_manual_steps_and_after_the_end()
    {
        var engine = Engine(TestScripts.Manual("10"));

        engine.RecordAutomatic(TestScripts.Clean).ShouldBeNull();
        engine.Next();

        engine.IsFinished.ShouldBeTrue();
        engine.RecordAutomatic(TestScripts.Clean).ShouldBeNull();
        engine.MarkWorked(TestScripts.Clean).ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => engine.MarkFailed(TestScripts.Clean));
    }

    [Fact]
    public void Repetir_discards_the_attempt_but_keeps_it_for_the_report()
    {
        var engine = Engine(TestScripts.Tap("1"));
        engine.RecordAutomatic(TestScripts.Activated);
        engine.RecordAutomatic(TestScripts.Clean);

        engine.Repeat();

        var step = engine.Snapshot().Steps[0];
        step.Repetitions.ShouldBeEmpty();
        step.Discarded.Length.ShouldBe(1);
        step.Discarded[0].Repetitions.Length.ShouldBe(2);
        step.Verdict.ShouldBe(StepVerdict.InProgress);

        for (var i = 0; i < 20; i++)
        {
            engine.RecordAutomatic(TestScripts.Clean);
        }

        engine.MarkWorked(RepetitionEvidence.Empty);
        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.Passed);
        engine.Snapshot().Steps[0].Repetitions[0].Index.ShouldBe(1);
    }

    [Fact]
    public void Repetir_on_an_untouched_step_discards_nothing()
    {
        var engine = Engine(TestScripts.Tap("1"));

        engine.Repeat();

        engine.Snapshot().Steps[0].Discarded.ShouldBeEmpty();
    }

    [Fact]
    public void Siguiente_without_repetitions_leaves_an_optional_step_not_applicable_and_a_required_one_incomplete()
    {
        var engine = Engine(TestScripts.Tap("19") with { Optional = true }, TestScripts.Tap("25"));

        engine.Next();
        engine.Next();

        var steps = engine.Snapshot().Steps;
        steps[0].Verdict.ShouldBe(StepVerdict.NotApplicable);
        steps[1].Verdict.ShouldBe(StepVerdict.Incomplete);
    }

    [Fact]
    public void Steps_not_reached_are_pending_and_the_current_one_is_in_progress()
    {
        var engine = Engine(TestScripts.Tap("1"), TestScripts.Tap("2"));

        var steps = engine.Snapshot().Steps;

        steps[0].Verdict.ShouldBe(StepVerdict.InProgress);
        steps[0].StartedAt.ShouldBe(_time.GetUtcNow());
        steps[1].Verdict.ShouldBe(StepVerdict.Pending);
        engine.CurrentStep!.Id.ShouldBe("1");
    }

    [Fact]
    public void The_spike_passes_when_every_step_passes_or_does_not_apply()
    {
        var engine = Engine(
            TestScripts.Manual("1", required: 1),
            TestScripts.Tap("2") with
            {
                Optional = true,
            }
        );

        engine.MarkWorked(RepetitionEvidence.Empty);
        engine.Next();
        engine.Snapshot().Verdict.ShouldBe(SpikeVerdict.Incomplete);
        engine.Next();

        engine.Snapshot().Verdict.ShouldBe(SpikeVerdict.Passed);
        engine.Snapshot().IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void The_spike_fails_when_a_decisive_step_fails()
    {
        var engine = Engine(
            TestScripts.Manual("1", required: 1),
            TestScripts.Manual("2", required: 1)
        );

        engine.MarkFailed(RepetitionEvidence.Empty);

        engine.Snapshot().Verdict.ShouldBe(SpikeVerdict.Failed);
    }

    [Fact]
    public void A_non_decisive_failure_does_not_decide_the_spike()
    {
        var engine = Engine(
            TestScripts.Manual("1", required: 1),
            TestScripts.Manual("32", required: 1) with
            {
                Decisive = false,
            }
        );

        engine.MarkWorked(RepetitionEvidence.Empty);
        engine.Next();
        engine.MarkFailed(RepetitionEvidence.Empty);
        engine.Next();

        engine.Snapshot().Steps[1].Verdict.ShouldBe(StepVerdict.Failed);
        engine.Snapshot().Verdict.ShouldBe(SpikeVerdict.Passed);
    }

    [Fact]
    public void GoTo_returns_to_a_step_left_behind()
    {
        var engine = Engine(
            TestScripts.Manual("1", required: 1),
            TestScripts.Manual("2", required: 1)
        );
        engine.Next();

        engine.GoTo(0);
        engine.MarkWorked(RepetitionEvidence.Empty);

        engine.CurrentStep!.Id.ShouldBe("1");
        engine.Snapshot().Steps[0].Verdict.ShouldBe(StepVerdict.Passed);
        engine.Snapshot().Steps[1].Verdict.ShouldBe(StepVerdict.Incomplete);
    }

    [Fact]
    public void Changed_fires_after_every_change()
    {
        var engine = Engine(TestScripts.Tap("1"), TestScripts.Tap("2"));
        var changes = 0;
        engine.Changed += (_, _) => changes++;

        engine.RecordAutomatic(TestScripts.Clean);
        engine.MarkWorked(RepetitionEvidence.Empty);
        engine.MarkFailed(RepetitionEvidence.Empty);
        engine.Repeat();
        engine.Next();

        changes.ShouldBe(5);
    }

    [Fact]
    public void A_script_without_steps_is_refused() =>
        Should.Throw<ArgumentException>(() =>
            new ScriptEngine(TestScripts.Script(), _time, TestScripts.RestoreBudget)
        );

    private ScriptEngine Engine(params ScriptStep[] steps) =>
        new(TestScripts.Script(steps), _time, TestScripts.RestoreBudget);
}
