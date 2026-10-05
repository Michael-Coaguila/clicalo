using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// Web, App and System actions (EJE-011, EJE-016): they never run on the engine thread. The effect goes to the Shell
/// thread, and the result comes back to the mailbox, possibly out of order; a result without a
/// pending effect (another engine's, or after a restart) is ignored.
/// </summary>
internal static class LaunchPlanner
{
    public static void Plan(EngineStep step, ExecutionOrigin origin, ShortcutAction action)
    {
        var effect = new EffectId(step.NextSequence());
        switch (action)
        {
            case UrlAction { Target: UrlTarget.Valid valid }:
                Pending(step, effect, origin, PendingKind.Url);
                step.Emit(
                    new EngineEffect.Launch(effect, new LaunchRequest.OpenUrl(valid.Address))
                );
                break;
            case AppAction app:
                Pending(step, effect, origin, PendingKind.App);
                step.Emit(new EngineEffect.Launch(effect, new LaunchRequest.StartApp(app.Target)));
                break;
            case SystemAction system:
                Pending(step, effect, origin, PendingKind.System);
                step.Emit(new EngineEffect.SystemCommand(effect, system.Command));
                break;
            default:
                return;
        }

        step.CountUsage(origin);
    }

    public static void Completed(EngineStep step, EffectId effect)
    {
        if (Take(step, effect) is { Kind: PendingKind.Url })
        {
            step.Notice(EngineNotices.Opened);
        }
    }

    public static void Failed(EngineStep step, EffectId effect, Failure failure)
    {
        if (Take(step, effect) is not null)
        {
            step.Notice(failure.Message, NoticeUrgency.Assertive);
        }
    }

    public static void SystemCompleted(EngineStep step, EffectId effect) => Take(step, effect);

    private static void Pending(
        EngineStep step,
        EffectId effect,
        ExecutionOrigin origin,
        PendingKind kind
    ) =>
        step.State = step.State with
        {
            PendingExternal = step.State.PendingExternal.SetItem(
                effect,
                new PendingExternal(effect, origin.Shortcut, step.Now)
                {
                    Kind = kind,
                    Origin = origin,
                }
            ),
        };

    private static PendingExternal? Take(EngineStep step, EffectId effect)
    {
        if (!step.State.PendingExternal.TryGetValue(effect, out var pending))
        {
            return null;
        }

        step.State = step.State with
        {
            PendingExternal = step.State.PendingExternal.Remove(effect),
        };
        return pending;
    }
}
