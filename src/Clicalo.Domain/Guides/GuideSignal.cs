namespace Clicalo.Domain.Guides;

/// <summary>
/// What the person did that a step waits for (R-39): only actions that are safe to ask for, never a shortcut that acts
/// on another app. A step that waits still offers [next].
/// </summary>
public enum GuideSignal
{
    /// <summary>★ showed the Frequents.</summary>
    FrequentsShown,

    /// <summary>The profile button went back to a profile or opened the profile grid.</summary>
    ProfileButtonUsed,

    /// <summary>The page of the panel changed.</summary>
    PageTurned,

    /// <summary>«−» turned the panel into the bubble.</summary>
    Minimized,

    /// <summary>The bubble brought the panel back.</summary>
    Restored,

    /// <summary>Another profile was chosen in Atajos.</summary>
    ProfileChosen,

    /// <summary>A touch landed on the test area of Precisión táctil.</summary>
    TestTapped,

    /// <summary>Another tab of Sistema was chosen.</summary>
    SystemTabChanged,
}
