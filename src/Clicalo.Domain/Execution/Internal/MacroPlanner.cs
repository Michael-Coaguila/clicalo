using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// Macros (EJE-010, blueprint §3.2 rule 4): a resumable state machine driven by timers, never a blocked thread. A keys
/// step is a Tap held by the macro's holder, a wait is a timer, a text step types, a mouse step acts; a drag step
/// toggles the left button on the macro's holder. A second tap cancels and releases everything the macro holds, and so
/// do «Release all», a real app switch and an elevated target.
/// </summary>
internal static class MacroPlanner
{
    /// <summary>Starts a macro, cancelling any other one that runs.</summary>
    public static void Start(EngineStep step, ExecutionOrigin origin, MacroAction macro)
    {
        if (step.State.Macro is { } running)
        {
            Cancel(step, running);
        }

        var run = new MacroRun(
            new MacroRunId(step.NextSequence()),
            origin.Shortcut,
            StepIndex: 0,
            macro.Steps.Count,
            WaitingUntilTicks: null
        )
        {
            Steps = macro.Steps,
            Origin = origin,
        };
        step.State = step.State with { Macro = run };
        step.AdvanceMacro();
    }

    /// <summary>A second tap on the running macro: cancel it and release what it holds (EJE-010).</summary>
    public static void Cancel(EngineStep step, MacroRun run) =>
        step.CancelHolder(EngineStep.MacroHolder(run));

    /// <summary>The wait timer fired.</summary>
    public static void WaitTimer(EngineStep step)
    {
        if (step.State.Macro is { WaitingUntilTicks: { } until } run && until <= step.Now)
        {
            step.State = step.State with { Macro = run with { WaitingUntilTicks = null } };
            step.AdvanceMacro();
        }
    }

    /// <summary>Runs steps until one has to wait (a wait step, or the queued keys of a keys step).</summary>
    public static void Advance(EngineStep step)
    {
        while (step.State.Macro is { WaitingUntilTicks: null, Origin: { } origin } run)
        {
            var holder = EngineStep.MacroHolder(run);
            if (step.State.Outbox.Items.Any(queued => queued.Holder == holder))
            {
                return;
            }

            if (run.StepIndex >= run.StepCount || run.StepIndex >= run.Steps.Count)
            {
                Finish(step, run, holder);
                return;
            }

            if (!step.CanPress(origin))
            {
                Cancel(step, run);
                return;
            }

            var current = run.Steps[run.StepIndex];
            step.State = step.State with { Macro = run with { StepIndex = run.StepIndex + 1 } };
            switch (current)
            {
                case KeysStep keys:
                    Keys(step, run, origin, holder, keys);
                    break;
                case WaitStep wait:
                    step.State = step.State with
                    {
                        Macro = step.State.Macro! with
                        {
                            WaitingUntilTicks = step.Now + step.Ticks(wait.Duration),
                        },
                    };
                    return;
                case TextStep text:
                    step.Emit(
                        new EngineEffect.TypeText(
                            new EffectId(step.NextSequence()),
                            text.Text,
                            origin.Epoch,
                            origin.RequiredForeground
                        )
                    );
                    break;
                case MouseStep mouse:
                    Mouse(step, origin, holder, mouse);
                    break;
            }
        }
    }

    private static void Keys(
        EngineStep step,
        MacroRun run,
        ExecutionOrigin origin,
        HolderId holder,
        KeysStep keys
    )
    {
        if (
            !KeyResolver.TryResolve(
                keys.Chord.Strokes,
                origin.Injection,
                step.Layout,
                out var resolved
            )
        )
        {
            Cancel(step, run);
            step.Notice(EngineNotices.Incomplete, NoticeUrgency.Assertive);
            return;
        }

        var (deadline, inherits) = step.GlobalDeadline();
        var template = KeyPlanner.Template(
            step,
            holder,
            HoldOrigin.Macro,
            origin,
            contact: null,
            deadline,
            inherits
        );
        step.Enqueue(
            KeyPlanner
                .PressThenLift(template, resolved, origin)
                .Append(
                    new QueuedStep.Finish(
                        holder,
                        new StepCompletion(origin, Notice: null, CountsUsage: false, run.Id)
                    )
                )
        );
    }

    private static void Mouse(
        EngineStep step,
        ExecutionOrigin origin,
        HolderId holder,
        MouseStep mouse
    )
    {
        if (mouse.Op != MouseOp.Drag)
        {
            step.Emit(
                new EngineEffect.MouseAction(
                    mouse.Op,
                    ScrollSpeed.Normal,
                    origin.ExternalPointer,
                    origin.Epoch
                )
            );
            return;
        }

        if (
            step.State.Keys.Items.TryGetValue(holder, out var item)
            && item.Buttons.HasFlag(MouseButtons.Left)
        )
        {
            step.Release(step.State.Keys.LiftButtons(holder, MouseButtons.Left), holder);
            return;
        }

        step.Emit(
            new EngineEffect.MouseAction(
                MouseOp.Drag,
                ScrollSpeed.Normal,
                origin.ExternalPointer,
                origin.Epoch
            )
        );
        var (deadline, inherits) = step.GlobalDeadline();
        var template = KeyPlanner.Template(
            step,
            holder,
            HoldOrigin.Macro,
            origin,
            contact: null,
            deadline,
            inherits
        );
        step.Press(step.State.Keys.PressButtons(template, MouseButtons.Left), holder, origin);
    }

    private static void Finish(EngineStep step, MacroRun run, HolderId holder)
    {
        step.Release(step.State.Keys.Release(holder), holder);
        step.State = step.State with { Macro = null };
        step.Notice(EngineNotices.MacroRan);
        if (run.Origin is { } origin)
        {
            step.CountUsage(origin);
        }
    }
}
