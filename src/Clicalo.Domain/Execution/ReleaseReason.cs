namespace Clicalo.Domain.Execution;

/// <summary>Why everything is released without ending the session (SEG-003, SEG-005).</summary>
public enum ReleaseReason
{
    /// <summary>The «Release all» button (panel, bar, tray).</summary>
    User,

    /// <summary>A real app switch with <c>safeSwitch</c> on.</summary>
    AppSwitch,

    /// <summary>The target became elevated while something was held.</summary>
    TargetElevated,
}
