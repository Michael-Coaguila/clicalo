namespace Clicalo.Domain.Execution;

/// <summary>Why a terminal event releases everything and cancels the macro (SEG-007, blueprint §7.6).</summary>
public enum TerminalReason
{
    /// <summary>Clícalo exits (<c>CleanShutdown</c>).</summary>
    Exit,

    /// <summary>The session is locked; what fails stays pending and is retried on unlock.</summary>
    Lock,

    /// <summary>The computer suspends (answered synchronously, <c>Timings.KeySafety.SuspendReleaseWait</c>).</summary>
    Suspend,

    /// <summary>The user signs out or the computer shuts down.</summary>
    SessionEnd,

    /// <summary>Clícalo relaunches elevated (<c>CleanShutdown | NoRelaunch</c>).</summary>
    Relaunch,

    /// <summary>An update is installed (only with nothing held).</summary>
    Update,

    /// <summary>The panel is hidden from the tray.</summary>
    Hide,

    /// <summary>Sending is paused.</summary>
    Pause,

    /// <summary>The panel view changes.</summary>
    ViewChange,

    /// <summary>The engine caught an exception: emergency release and an empty state (NFR-005).</summary>
    EngineFault,
}
