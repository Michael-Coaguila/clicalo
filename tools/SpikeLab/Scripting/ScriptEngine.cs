using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// Runs one spike script step by step: counts the repetitions (automatic or marked by the maintainer), applies the
/// automatic checks of each step, and keeps «Repetir» and «Siguiente» honest (a discarded attempt stays in the
/// report). Single-threaded: it is used from the UI thread only. <see cref="Changed"/> fires after every change.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>
/// Automatic steps (<see cref="ScriptStep.NeedsConfirmation"/>): every trigger that matches the step is one
/// repetition (<see cref="RecordAutomatic"/>). «Funcionó» confirms the final check the laboratory cannot see (the
/// dictated word reached the app); «Falló» marks the last repetition as failed by the maintainer, or adds a failed
/// one when there is none.
/// </item>
/// <item>Manual steps: every «Funcionó» or «Falló» is one repetition, with the evidence measured since the previous one.</item>
/// <item>A repetition whose evidence breaks one of the step's checks fails even after «Funcionó».</item>
/// </list>
/// </remarks>
internal sealed class ScriptEngine
{
    private const string UserFailure = "El mantenedor marcó «Falló».";

    private readonly TimeProvider _time;
    private readonly TimeSpan _restoreBudget;
    private ImmutableArray<StepProgress> _steps;
    private int _current;

    /// <summary>Starts <paramref name="script"/> at its first step.</summary>
    /// <param name="script">The script to run.</param>
    /// <param name="time">Stamps the repetitions.</param>
    /// <param name="restoreBudget">The limit of the forced-activation check (<c>Timings.Windowing.ViolationRestoreBudget</c>).</param>
    public ScriptEngine(SpikeScript script, TimeProvider time, TimeSpan restoreBudget)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(time);
        if (script.Steps.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A script needs at least one step.", nameof(script));
        }

        Script = script;
        _time = time;
        _restoreBudget = restoreBudget;
        StartedAt = time.GetUtcNow();
        _steps = [.. script.Steps.Select(step => new StepProgress(step))];
        _steps = _steps.SetItem(0, _steps[0] with { Visited = true, StartedAt = StartedAt });
    }

    /// <summary>Raised after every change of the run.</summary>
    public event EventHandler? Changed;

    /// <summary>The script being run.</summary>
    public SpikeScript Script { get; }

    /// <summary>When the run started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>The current step; null once the script is finished.</summary>
    public ScriptStep? CurrentStep => IsFinished ? null : _steps[_current].Step;

    /// <summary>True once the maintainer moved past the last step.</summary>
    public bool IsFinished => _current >= _steps.Length;

    /// <summary>An immutable picture of the run.</summary>
    public ScriptSnapshot Snapshot() => new(Script, _steps, _current, StartedAt);

    /// <summary>
    /// Records one repetition counted by the laboratory. Ignored (null) on a manual step or after the end: the caller
    /// has already checked that the trigger matches the step (<see cref="StepTriggerMatcher"/>).
    /// </summary>
    public RepetitionRecord? RecordAutomatic(RepetitionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (IsFinished || !_steps[_current].Step.NeedsConfirmation)
        {
            return null;
        }

        return Append(RepetitionSource.Automatic, evidence, userFailed: false);
    }

    /// <summary>
    /// «Funcionó». On a manual step it records a repetition with <paramref name="evidence"/> (it still fails if the
    /// evidence breaks a check); on an automatic step it confirms the final check and returns null.
    /// </summary>
    public RepetitionRecord? MarkWorked(RepetitionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (IsFinished)
        {
            return null;
        }

        var progress = _steps[_current];
        if (progress.Step.NeedsConfirmation)
        {
            Replace(progress with { Confirmed = true });
            return null;
        }

        return Append(RepetitionSource.User, evidence, userFailed: false);
    }

    /// <summary>
    /// «Falló». On a manual step it records a failed repetition. On an automatic step it marks the last repetition as
    /// failed (the maintainer saw what the laboratory cannot measure, such as a menu that closed), or records a failed
    /// one when there is none yet, and withdraws any confirmation.
    /// </summary>
    public RepetitionRecord MarkFailed(RepetitionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (IsFinished)
        {
            throw new InvalidOperationException("The script is finished.");
        }

        var progress = _steps[_current];
        if (progress.Step.NeedsConfirmation && !progress.Repetitions.IsEmpty)
        {
            var last = progress.Repetitions[^1];
            if (!last.FailedByUser)
            {
                var failed = last with
                {
                    Passed = false,
                    FailedByUser = true,
                    Problems = last.Problems.Add(UserFailure),
                };
                Replace(
                    progress with
                    {
                        Repetitions = progress.Repetitions.SetItem(
                            progress.Repetitions.Length - 1,
                            failed
                        ),
                        Confirmed = false,
                    }
                );
                return failed;
            }
        }

        return Append(RepetitionSource.User, evidence, userFailed: true);
    }

    /// <summary>
    /// «Repetir»: starts the current step again. Its repetitions move to <see cref="StepProgress.Discarded"/> (they stay
    /// in the report) and the confirmation is withdrawn.
    /// </summary>
    public void Repeat()
    {
        if (IsFinished)
        {
            return;
        }

        var progress = _steps[_current];
        var discarded = progress.Discarded;
        if (!progress.Repetitions.IsEmpty || progress.Confirmed)
        {
            discarded = discarded.Add(
                new DiscardedAttempt(_time.GetUtcNow(), progress.Repetitions, progress.Confirmed)
            );
        }

        Replace(
            progress with
            {
                Repetitions = [],
                Discarded = discarded,
                Confirmed = false,
                Left = false,
            }
        );
    }

    /// <summary>«Siguiente»: leaves the current step as it is and makes the next one current.</summary>
    public void Next()
    {
        if (IsFinished)
        {
            return;
        }

        _steps = _steps.SetItem(_current, _steps[_current] with { Left = true });
        _current++;
        if (!IsFinished)
        {
            var next = _steps[_current];
            _steps = _steps.SetItem(
                _current,
                next with
                {
                    Visited = true,
                    Left = false,
                    StartedAt = next.StartedAt ?? _time.GetUtcNow(),
                }
            );
        }

        OnChanged();
    }

    /// <summary>Makes step <paramref name="index"/> current again (to redo or finish a step left behind).</summary>
    public void GoTo(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _steps.Length);
        if (!IsFinished)
        {
            _steps = _steps.SetItem(_current, _steps[_current] with { Left = true });
        }

        _current = index;
        var target = _steps[index];
        _steps = _steps.SetItem(
            index,
            target with
            {
                Visited = true,
                Left = false,
                StartedAt = target.StartedAt ?? _time.GetUtcNow(),
            }
        );
        OnChanged();
    }

    private RepetitionRecord Append(
        RepetitionSource source,
        RepetitionEvidence evidence,
        bool userFailed
    )
    {
        var progress = _steps[_current];
        var problems = EvidenceEvaluator.Evaluate(progress.Step.Checks, evidence, _restoreBudget);
        if (userFailed)
        {
            problems = problems.Add(UserFailure);
        }

        var record = new RepetitionRecord(
            progress.Repetitions.Length + 1,
            _time.GetUtcNow(),
            source,
            problems.IsEmpty,
            problems,
            evidence
        )
        {
            FailedByUser = userFailed,
        };
        Replace(progress with { Repetitions = progress.Repetitions.Add(record) });
        return record;
    }

    private void Replace(StepProgress progress)
    {
        _steps = _steps.SetItem(_current, progress);
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
