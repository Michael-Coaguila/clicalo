using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Coordinators;

/// <summary>Data of <see cref="EngineObserverRelay.UsageCounted"/>.</summary>
/// <param name="shortcut">The shortcut that ran.</param>
/// <param name="at">When it ran.</param>
public sealed class UsageCountedEventArgs(ShortcutId shortcut, DateTimeOffset at) : EventArgs
{
    /// <summary>The shortcut that ran.</summary>
    public ShortcutId Shortcut { get; } = shortcut;

    /// <summary>When it ran.</summary>
    public DateTimeOffset At { get; } = at;
}
