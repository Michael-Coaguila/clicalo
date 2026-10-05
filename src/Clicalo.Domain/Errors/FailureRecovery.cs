namespace Clicalo.Domain.Errors;

/// <summary>What the UI offers next to an <see cref="Failure"/> (blueprint §6.1).</summary>
public enum FailureRecovery
{
    /// <summary>Nothing to offer.</summary>
    None,

    /// <summary>Try the same operation again.</summary>
    Retry,

    /// <summary>Open the setting that causes it.</summary>
    OpenSettings,

    /// <summary>Restore a backup.</summary>
    RestoreBackup,

    /// <summary>Relaunch Clícalo (for example, elevated).</summary>
    Relaunch,

    /// <summary>Go back to the previous version.</summary>
    Rollback,
}
