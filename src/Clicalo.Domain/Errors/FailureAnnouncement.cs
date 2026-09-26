namespace Clicalo.Domain.Errors;

/// <summary>How a screen reader announces an <see cref="Failure"/> (REG-06).</summary>
public enum FailureAnnouncement
{
    /// <summary>Waits for the reader to finish.</summary>
    Polite,

    /// <summary>Interrupts the reader.</summary>
    Assertive,
}
