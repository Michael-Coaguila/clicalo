namespace Clicalo.Application.Persistence;

/// <summary>Whether the user's changes are on disk (DAT-002: a persistent failure is visible, never only logged).</summary>
public enum SaveStatus
{
    /// <summary>Everything is saved.</summary>
    Saved,

    /// <summary>A save is scheduled (debounce).</summary>
    Pending,

    /// <summary>A transient error is being retried.</summary>
    Retrying,

    /// <summary>
    /// The error persists: «not saved» is shown in the panel and the Control Center status bar, the write is retried
    /// every <c>Timings.Persistence.WriteRetryInterval</c> and an emergency copy goes to <c>pending\</c>.
    /// </summary>
    Failing,
}
