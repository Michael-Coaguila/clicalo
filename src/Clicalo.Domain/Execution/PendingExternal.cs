using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution;

/// <summary>An external effect sent to another thread whose result has not come back yet (results may arrive out of order).</summary>
/// <param name="Id">The effect.</param>
/// <param name="Shortcut">The shortcut that caused it.</param>
/// <param name="SinceTicks">When it was sent.</param>
public sealed record PendingExternal(EffectId Id, ShortcutId Shortcut, long SinceTicks)
{
    /// <summary>What the effect is (a paste sends Ctrl+V when the clipboard is ready).</summary>
    public PendingKind Kind { get; init; }

    /// <summary>Where and how it was started (a paste sends Ctrl+V with this epoch, target and mode).</summary>
    public ExecutionOrigin? Origin { get; init; }
}
