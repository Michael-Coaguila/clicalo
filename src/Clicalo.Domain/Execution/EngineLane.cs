namespace Clicalo.Domain.Execution;

/// <summary>
/// The two lanes of the engine mailbox (blueprint §3.2, rule 3): <see cref="Priority"/> is always drained before
/// <see cref="Normal"/>, so releasing never waits behind a macro.
/// </summary>
public enum EngineLane
{
    /// <summary>Release all, terminal events, end of contact, real app switches.</summary>
    Priority,

    /// <summary>Everything else.</summary>
    Normal,
}
