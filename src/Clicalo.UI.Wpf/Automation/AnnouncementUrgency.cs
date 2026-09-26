namespace Clicalo.UI.Wpf.Automation;

/// <summary>How urgently screen readers announce a notice (ACC-001, blueprint §8.6).</summary>
public enum AnnouncementUrgency
{
    /// <summary>
    /// Waits for the current speech: the notice bar and the status bar (<c>LiveSetting.Polite</c>,
    /// <c>AutomationNotificationProcessing.ImportantMostRecent</c>).
    /// </summary>
    Polite,

    /// <summary>
    /// Interrupts: the panic strip, errors and «No pude volver a {app}» (<c>LiveSetting.Assertive</c>,
    /// <c>AutomationNotificationProcessing.ImportantAll</c>).
    /// </summary>
    Assertive,
}
