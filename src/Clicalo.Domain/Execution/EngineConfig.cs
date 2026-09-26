using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>The settings the engine obeys, projected from the user document by the host.</summary>
/// <param name="MaxHold">Global automatic release limit, or <see langword="null"/> for «Never» (SEG-004).</param>
/// <param name="ReleaseOnAppSwitch">Release everything on a real app switch (SEG-005).</param>
/// <param name="InterEventDelay">Delay between injected events; zero sends one atomic batch (<c>Timings.Injection.InterEventDelay</c>).</param>
/// <param name="Touch">The touch filter.</param>
public sealed record EngineConfig(
    TimeSpan? MaxHold,
    bool ReleaseOnAppSwitch,
    TimeSpan InterEventDelay,
    TouchSettings Touch
);
