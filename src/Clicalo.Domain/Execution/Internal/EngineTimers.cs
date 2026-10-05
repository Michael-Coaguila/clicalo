namespace Clicalo.Domain.Execution.Internal;

/// <summary>The engine's timers: one per purpose, moved rather than duplicated (blueprint §7.4: a single deadline timer).</summary>
internal static class EngineTimers
{
    /// <summary>The earliest automatic release of a held item (SEG-004).</summary>
    public static readonly TimerKey Deadline = new("deadline");

    /// <summary>The next queued key step (the pause between events, SEG-008).</summary>
    public static readonly TimerKey Outbox = new("outbox");

    /// <summary>The end of an armed confirmation (EJE-002).</summary>
    public static readonly TimerKey Confirm = new("confirm");

    /// <summary>The end of a macro wait step (EJE-010).</summary>
    public static readonly TimerKey Macro = new("macro");

    /// <summary>The next wheel step of a repeating scroll (EJE-009).</summary>
    public static readonly TimerKey Scroll = new("scroll");
}
