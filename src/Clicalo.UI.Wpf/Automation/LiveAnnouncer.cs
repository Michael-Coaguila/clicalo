using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// Announces notices to screen readers from a non-activatable surface (blueprint §8.6, ACC-001, REG-06, S3): it
/// sets the text of a live region element (<c>AutomationProperties.LiveSetting</c> Polite or Assertive), raises
/// <c>LiveRegionChanged</c> on its peer and, for readers that ignore live regions on inactive windows, also
/// <c>RaiseNotificationEvent</c> with <see cref="ActivityId"/>. Runs on the UI thread of the region.
/// </summary>
/// <remarks>
/// <para>The text arrives localized (a notice's <c>MessageKey</c> rendered by the localizer); never a literal.</para>
/// <para>
/// The region shows the text itself when it is a <see cref="TextBlock"/> (its <c>Text</c>, which is also its UI
/// Automation name) or a <see cref="ContentControl"/> (its <c>Content</c> and name). Any other element receives the
/// text as its <c>AutomationProperties.Name</c> and presents it by its own means. The region needs a UI Automation
/// peer and must be a content element of the tree (not a part of a control template).
/// </para>
/// <para>
/// Announcing an empty text clears the region without raising anything: silence is never announced. Nothing here
/// moves the keyboard focus or the foreground (REG-01).
/// </para>
/// </remarks>
public sealed class LiveAnnouncer
{
    /// <summary>Activity id of every notification, so readers can group or replace Clícalo's notices.</summary>
    public const string ActivityId = "Clicalo.Notice";

    /// <summary>
    /// Creates an announcer that speaks through <paramref name="region"/>, which is declared a polite live region
    /// at once (AVI-001) unless it already has a live setting. Call it on the region's UI thread.
    /// </summary>
    /// <param name="region">
    /// The element that shows the notice (the notice bar or status bar text); it becomes the live region.
    /// </param>
    public LiveAnnouncer(FrameworkElement region)
    {
        ArgumentNullException.ThrowIfNull(region);
        region.VerifyAccess();
        Region = region;
        if (AutomationProperties.GetLiveSetting(region) == AutomationLiveSetting.Off)
        {
            AutomationProperties.SetLiveSetting(region, AutomationLiveSetting.Polite);
        }
    }

    /// <summary>The live region element.</summary>
    public FrameworkElement Region { get; }

    /// <summary>The last text announced; empty before the first announcement.</summary>
    public string LastText { get; private set; } = string.Empty;

    /// <summary>The urgency of the last announcement; <see cref="AnnouncementUrgency.Polite"/> before the first.</summary>
    public AnnouncementUrgency LastUrgency { get; private set; }

    /// <summary>How many non-empty texts have been announced.</summary>
    public int Announcements { get; private set; }

    /// <summary>Shows and announces <paramref name="text"/> with <paramref name="urgency"/>.</summary>
    /// <param name="text">The localized notice text.</param>
    /// <param name="urgency">Polite or assertive.</param>
    public void Announce(string text, AnnouncementUrgency urgency)
    {
        ArgumentNullException.ThrowIfNull(text);
        Region.VerifyAccess();
        var (liveSetting, processing) = urgency switch
        {
            AnnouncementUrgency.Polite => (
                AutomationLiveSetting.Polite,
                AutomationNotificationProcessing.MostRecent
            ),
            AnnouncementUrgency.Assertive => (
                AutomationLiveSetting.Assertive,
                AutomationNotificationProcessing.ImportantAll
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, message: null),
        };

        AutomationProperties.SetLiveSetting(Region, liveSetting);
        Show(text);
        LastText = text;
        LastUrgency = urgency;
        if (text.Length == 0)
        {
            return;
        }

        Announcements++;
        var peer =
            UIElementAutomationPeer.FromElement(Region)
            ?? UIElementAutomationPeer.CreatePeerForElement(Region);
        if (peer is null)
        {
            return;
        }

        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        peer.RaiseNotificationEvent(AutomationNotificationKind.Other, processing, text, ActivityId);
    }

    private void Show(string text)
    {
        switch (Region)
        {
            case TextBlock block:
                block.Text = text;
                break;
            case ContentControl content:
                content.Content = text;
                AutomationProperties.SetName(content, text);
                break;
            default:
                AutomationProperties.SetName(Region, text);
                break;
        }
    }
}
