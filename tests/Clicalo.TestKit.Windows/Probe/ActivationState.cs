namespace Clicalo.TestKit.Windows.Probe;

/// <summary>The low word of <c>WM_ACTIVATE</c>'s <c>wParam</c>.</summary>
public enum ActivationState
{
    /// <summary><c>WA_INACTIVE</c>.</summary>
    Inactive = 0,

    /// <summary><c>WA_ACTIVE</c>: activated by something other than a click (keyboard, <c>SetForegroundWindow</c>...).</summary>
    Active = 1,

    /// <summary><c>WA_CLICKACTIVE</c>: activated by a mouse click.</summary>
    ClickActive = 2,
}
