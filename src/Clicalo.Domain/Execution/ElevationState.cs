namespace Clicalo.Domain.Execution;

/// <summary>Whether Clícalo may send to the foreground app (EJE-013).</summary>
public enum ElevationState
{
    /// <summary>The foreground app's integrity could not be read (protected process, EC-PER-03): try, and warn if it fails.</summary>
    Unknown,

    /// <summary>The app is not above Clícalo.</summary>
    Allowed,

    /// <summary>The app is elevated and Clícalo is not: nothing is sent.</summary>
    TargetElevated,
}
