using Clicalo.Application.Engine;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Interaction;

/// <summary>
/// How the notices of the engine enter the <see cref="NoticeQueue"/> (AVI-002), pure: which ones are safety notices,
/// and the two fixed notices that last while the engine's state lasts, read from its snapshot so they end exactly
/// when the state does: a Mantener under a finger and a running macro.
/// </summary>
public static class EngineNoticeRules
{
    private static readonly MessageKey HoldingKey = L.HoldingKeys(keys: string.Empty).Key;
    private static readonly MessageKey MacroRunningKey = L.MacroRunning(
        name: string.Empty,
        index: 1,
        total: 1
    ).Key;

    private static readonly HashSet<MessageKey> Safety =
    [
        L.ReleasedAuto(count: 1).Key,
        L.ReleasedSwitch.Key,
        L.ReleasedOnLock.Key,
        L.EngineFault.Key,
        L.ElevatedRefused(app: string.Empty).Key,
    ];

    /// <summary>
    /// The kind of a notice of the engine: the ones that tell why keys were released by themselves or why nothing was
    /// sent to an elevated app are safety notices and are never lost (AVI-002); the rest are plain.
    /// </summary>
    /// <param name="text">The notice.</param>
    public static NoticeKind KindOf(Message text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Safety.Contains(text.Key) ? NoticeKind.Safety : NoticeKind.Normal;
    }

    /// <summary>
    /// Whether <paramref name="text"/> tells a state that lasts («Manteniendo…», «Ejecutando…»): the fixed notice read
    /// from the snapshot tells it for as long as it lasts, so it is not posted as a notice of 3,2 s too.
    /// </summary>
    /// <param name="text">The notice.</param>
    public static bool IsProgress(Message text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Key == HoldingKey || text.Key == MacroRunningKey;
    }

    /// <summary>The fixed notice of a Mantener held under a finger (EJE-004), or null when none is.</summary>
    /// <param name="snapshot">The engine snapshot.</param>
    public static Message? HoldInProgress(EngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var item in snapshot.Held)
        {
            if (item.ContactId is not null)
            {
                return item.Label is { Length: > 0 } keys ? L.HoldingKeys(keys: keys) : L.Holding;
            }
        }

        return null;
    }

    /// <summary>The fixed notice of the running macro with its step (EJE-010), or null when none runs.</summary>
    /// <param name="snapshot">The engine snapshot.</param>
    public static Message? MacroInProgress(EngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Macro is { StepCount: > 0 } run
            ? L.MacroRunning(
                name: run.Name,
                index: Math.Clamp(run.StepIndex + 1, 1, run.StepCount),
                total: run.StepCount
            )
            : null;
    }
}
