using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Engine;

/// <summary>
/// Receives the engine's outputs (blueprint §7.1: snapshot, notices, usage, last action). Called on the engine thread:
/// implementations only queue to their own thread and never block.
/// </summary>
public interface IEngineObserver
{
    /// <summary>A new snapshot (coalesced to one per frame).</summary>
    /// <param name="snapshot">The snapshot.</param>
    void OnSnapshot(EngineSnapshot snapshot);

    /// <summary>A notice for the panel and screen readers.</summary>
    /// <param name="text">The text.</param>
    /// <param name="urgency">How it is announced.</param>
    void OnNotice(Message text, NoticeUrgency urgency);

    /// <summary>An effective execution to count in Frequents (dispatched as <c>RecordUsage</c>, FRE-002).</summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="at">When.</param>
    void OnUsage(ShortcutId shortcut, DateTimeOffset at);

    /// <summary>The last action, for Repeat (AVI-004).</summary>
    /// <param name="shortcut">The shortcut.</param>
    void OnLastAction(ShortcutId shortcut);
}
