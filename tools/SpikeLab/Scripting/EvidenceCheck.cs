namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// The automatic checks of one repetition (<see cref="EvidenceEvaluator"/>). A repetition whose evidence breaks one of
/// its step's checks fails even if the maintainer said «Funcionó».
/// </summary>
[Flags]
internal enum EvidenceCheck
{
    /// <summary>No automatic check.</summary>
    None = 0,

    /// <summary>No lab surface received <c>WM_ACTIVATE</c>, <c>WM_NCACTIVATE(TRUE)</c> or <c>WM_ACTIVATEAPP(TRUE)</c>.</summary>
    NoSurfaceActivation = 1 << 0,

    /// <summary>The foreground never became a window of the laboratory.</summary>
    NoOwnForeground = 1 << 1,

    /// <summary><c>reg01.violations</c> did not increase.</summary>
    NoViolation = 1 << 2,

    /// <summary>The app that was in front when the step started is still in front.</summary>
    TargetStillInFront = 1 << 3,

    /// <summary>Forced activation: <c>reg01.violations</c> increased by exactly one.</summary>
    ViolationCountedOnce = 1 << 4,

    /// <summary>Forced activation: the previous foreground came back within <c>Timings.Windowing.ViolationRestoreBudget</c>.</summary>
    RestoredWithinBudget = 1 << 5,

    /// <summary>Forced activation: the panel still has <c>WS_EX_NOACTIVATE</c>.</summary>
    NoActivateStyleKept = 1 << 6,

    /// <summary>The lease was granted.</summary>
    LeaseGranted = 1 << 7,

    /// <summary>The lease gave the foreground back (Restored or RestoredAfterRetry) to the window in front before it.</summary>
    ForegroundReturned = 1 << 8,

    /// <summary>Every text field of the cycle received text (only lengths are measured, never the text).</summary>
    TextReachedField = 1 << 9,

    /// <summary>InputProbe received no <c>VK_F24</c>, no character and no <c>SC_KEYMENU</c>.</summary>
    ProbeSilent = 1 << 10,

    /// <summary>InputProbe was the window in front when the lease was requested.</summary>
    ProbeWasTarget = 1 << 11,

    /// <summary>The command reached a tile whose accessible name started with its voice number (ACC-009).</summary>
    VoiceNumberInName = 1 << 12,

    /// <summary>The checks of every S1 and S3 cycle: nothing of the laboratory took the foreground or the focus.</summary>
    NonActivation = NoSurfaceActivation | NoOwnForeground | NoViolation | TargetStillInFront,

    /// <summary>The checks of the forced activation of S1 row 31.</summary>
    ForcedActivationReverted = ViolationCountedOnce | RestoredWithinBudget | NoActivateStyleKept,

    /// <summary>The checks of every S4 cycle.</summary>
    LeaseRoundTrip = LeaseGranted | ForegroundReturned | NoViolation,
}
