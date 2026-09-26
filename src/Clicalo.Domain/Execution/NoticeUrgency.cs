namespace Clicalo.Domain.Execution;

/// <summary>How an engine notice is announced (REG-06).</summary>
public enum NoticeUrgency
{
    /// <summary>Waits for the screen reader.</summary>
    Polite,

    /// <summary>Interrupts the screen reader (panic, released keys).</summary>
    Assertive,
}
