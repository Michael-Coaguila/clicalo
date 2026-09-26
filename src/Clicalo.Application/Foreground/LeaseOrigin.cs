namespace Clicalo.Application.Foreground;

/// <summary>
/// What triggered a lease request; it selects the step of the foreground rights ladder (blueprint §3.6). Windows
/// only lets the process that received the last input event call <c>SetForegroundWindow</c>.
/// </summary>
public enum LeaseOrigin
{
    /// <summary>A finger, pen or mouse on a surface: Clícalo received the input, so step 1 works (one retry).</summary>
    Touch,

    /// <summary>
    /// A UI Automation Invoke (Voice access, Narrator, Windows Speech Recognition, switches): Clícalo did not receive
    /// input, so step 2 (the internal rights hotkey) may be needed.
    /// </summary>
    UiaInvoke,

    /// <summary>The configurable global shortcut: <c>WM_HOTKEY</c> gives the right (step 1, one retry).</summary>
    GlobalHotkey,

    /// <summary>The tray icon: the click gives the right (step 1, one retry).</summary>
    Tray,

    /// <summary>Internal timers («Try now»): step 1 only, then flash or fail depending on the lease kind.</summary>
    Internal,
}
