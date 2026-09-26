namespace Clicalo.Platform.Core.Guardian;

/// <summary>What woke Sentinel up (blueprint §3.1: it waits on the parent process and on the heartbeat pipe).</summary>
public enum GuardianWake
{
    /// <summary>The main process ended (any way: exit, crash, <c>TerminateProcess</c>).</summary>
    ParentExited,

    /// <summary>The heartbeat pipe broke: the main process closed its end or died.</summary>
    PipeBroken,
}
