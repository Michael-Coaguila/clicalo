using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Primitives;
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
)
{
    /// <summary>
    /// Ticks per second of the host's <see cref="TimeProvider"/> (<see cref="TimeProvider.TimestampFrequency"/>): the
    /// reducer counts in those ticks. <see cref="TimeSpan.TicksPerSecond"/> by default, as the fake time provider.
    /// </summary>
    public long TimestampFrequency { get; init; } = TimeSpan.TicksPerSecond;

    /// <summary>
    /// The language of the target apps (keyboard settings), which picks a Tap's combination variant (docs/02
    /// <c>vk</c>); <see langword="null"/> uses the saved combination.
    /// </summary>
    public LangCode? AppsLanguage { get; init; }

    /// <summary>
    /// The adaptive common actions (decision D4): a tap of a common action sends the combination of the app in front
    /// and <see cref="AppsLanguage"/>. Empty by default: every tap is sent as saved.
    /// </summary>
    public CommonActionTable CommonActions { get; init; } = CommonActionTable.Empty;
}
