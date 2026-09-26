using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution;

/// <summary>An external effect sent to another thread whose result has not come back yet (results may arrive out of order).</summary>
/// <param name="Id">The effect.</param>
/// <param name="Shortcut">The shortcut that caused it.</param>
/// <param name="SinceTicks">When it was sent.</param>
public sealed record PendingExternal(EffectId Id, ShortcutId Shortcut, long SinceTicks);
