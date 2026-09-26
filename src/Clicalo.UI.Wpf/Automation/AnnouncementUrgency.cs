namespace Clicalo.UI.Wpf.Automation;

/// <summary>How urgently screen readers announce a notice (ACC-001, blueprint §8.6).</summary>
public enum AnnouncementUrgency
{
    /// <summary>
    /// Waits for the current speech, and a newer notice replaces an older one still waiting: the notice bar and the
    /// status bar (<c>LiveSetting.Polite</c>, <c>AutomationNotificationProcessing.MostRecent</c>).
    /// </summary>
    Polite,

    /// <summary>
    /// Interrupts, and none is dropped: the panic strip, errors and «No pude volver a {app}»
    /// (<c>LiveSetting.Assertive</c>, <c>AutomationNotificationProcessing.ImportantAll</c>).
    /// </summary>
    Assertive,
}
