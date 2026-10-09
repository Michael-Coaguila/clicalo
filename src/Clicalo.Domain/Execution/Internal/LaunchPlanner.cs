using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// Web, App and System actions (EJE-011, EJE-016): they never run on the engine thread. The effect goes to the Shell
/// thread, and the result comes back to the mailbox, possibly out of order; a result without a
/// pending effect (another engine's, or after a restart) is ignored.
/// </summary>
internal static class LaunchPlanner
{
    /// <summary>
    /// Plans <paramref name="action"/> (the shortcut's own, or the system action that replaces a blocked combination):
    /// a web or an app that is not safe to start never leaves the engine (LOG-008).
    /// </summary>
    public static void Plan(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        ShortcutAction action
    )
    {
        var name = step.NameOf(shortcut);
        switch (action)
        {
            case UrlAction { Target: UrlTarget.Valid valid }:
                Launch(
                    step,
                    origin,
                    shortcut,
                    new LaunchRequest.OpenUrl(valid.Address),
                    PendingKind.Url,
                    valid.Address.OriginalString
                );
                return;
            case AppAction app:
                Launch(
                    step,
                    origin,
                    shortcut,
                    new LaunchRequest.StartApp(app.Target),
                    PendingKind.App,
                    name
                );
                return;
            case SystemAction system:
            {
                var effect = new EffectId(step.NextSequence());
                Pending(step, effect, origin, PendingKind.System, name);
                step.Emit(new EngineEffect.SystemCommand(effect, system.Command));
                break;
            }
            default:
                return;
        }

        step.CountUsage(origin);
    }

    private static void Launch(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        LaunchRequest request,
        PendingKind kind,
        string name
    )
    {
        if (LaunchSafety.Check(request, shortcut.Options.Confirm) != LaunchVerdict.Allowed)
        {
            step.Notice(L.LaunchUnsafe(name: name), NoticeUrgency.Assertive);
            return;
        }

        var effect = new EffectId(step.NextSequence());
        Pending(step, effect, origin, kind, name);
        step.Emit(new EngineEffect.Launch(effect, request));
        step.CountUsage(origin);
    }

    public static void Completed(EngineStep step, EffectId effect)
    {
        if (Take(step, effect) is { Kind: PendingKind.Url or PendingKind.App } pending)
        {
            step.Notice(L.OpenedName(name: pending.Name));
        }
    }

    public static void Failed(EngineStep step, EffectId effect, Failure failure)
    {
        if (Take(step, effect) is not null)
        {
            step.Notice(failure.Message, NoticeUrgency.Assertive);
        }
    }

    public static void SystemCompleted(EngineStep step, EffectId effect, bool succeeded)
    {
        if (Take(step, effect) is { } pending && !succeeded)
        {
            step.Notice(L.ActionFailed(name: pending.Name), NoticeUrgency.Assertive);
        }
    }

    private static void Pending(
        EngineStep step,
        EffectId effect,
        ExecutionOrigin origin,
        PendingKind kind,
        string name
    ) =>
        step.State = step.State with
        {
            PendingExternal = step.State.PendingExternal.SetItem(
                effect,
                new PendingExternal(effect, origin.Shortcut, step.Now)
                {
                    Kind = kind,
                    Origin = origin,
                    Name = name,
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
