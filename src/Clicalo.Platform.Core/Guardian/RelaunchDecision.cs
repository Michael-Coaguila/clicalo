namespace Clicalo.Platform.Core.Guardian;

/// <summary>What happens after the main process ends (blueprint §3.1).</summary>
public enum RelaunchDecision
{
    /// <summary>Do not relaunch: the main process exited with code 0, or the crash loop was passed.</summary>
    None,

    /// <summary>Relaunch normally.</summary>
    Relaunch,

    /// <summary>The crash loop threshold was reached: relaunch into safe mode.</summary>
    RelaunchInSafeMode,
}
