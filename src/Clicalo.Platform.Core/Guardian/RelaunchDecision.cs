namespace Clicalo.Platform.Core.Guardian;

/// <summary>What happens after the main process dies (blueprint §3.1).</summary>
public enum RelaunchDecision
{
    /// <summary>Do not relaunch (<c>CleanShutdown</c> or <c>NoRelaunch</c>).</summary>
    None,

    /// <summary>Relaunch normally.</summary>
    Relaunch,

    /// <summary>The crash loop threshold was reached: relaunch into safe mode.</summary>
    RelaunchInSafeMode,
}
