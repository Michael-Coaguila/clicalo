namespace Clicalo.App;

/// <summary>Exit codes of <c>Clicalo.exe</c> (read by <c>cl run</c>, the S5 measurements and Sentinel's journal).</summary>
public enum AppExitCode
{
    /// <summary>Normal exit, or a second start that showed the running instance.</summary>
    Ok = 0,

    /// <summary>A second start could not reach the running instance in time.</summary>
    InstanceUnreachable = 2,

    /// <summary>A process that is not Clícalo holds the single-instance pipe (<c>ipc.squat_detected</c>).</summary>
    InstanceSquatted = 3,

    /// <summary>The start failed before the first frame (the log says why).</summary>
    StartupFailed = 70,
}
