using Clicalo.Application.Ports;

namespace Clicalo.Application.Foreground;

/// <summary>
/// The last VERIFIED external foreground and its epoch (blueprint §3.6): where Clícalo sends input, and where it
/// returns after a lease or a violation. Immutable; published by the orchestrator for readers on any thread.
/// </summary>
/// <param name="Window">The external foreground window; <see cref="WindowToken.None"/> before the first one.</param>
/// <param name="Epoch">The epoch in which it became the foreground.</param>
/// <param name="VerifiedAt">When <c>GetForegroundWindow</c> confirmed it.</param>
public sealed record ForegroundSnapshot(
    WindowToken Window,
    ForegroundEpoch Epoch,
    DateTimeOffset VerifiedAt
)
{
    /// <summary>No external foreground seen yet.</summary>
    public static ForegroundSnapshot Empty { get; } =
        new(WindowToken.None, default, DateTimeOffset.MinValue);
}
