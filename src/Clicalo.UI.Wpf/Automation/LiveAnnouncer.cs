using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

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
/// peer (a Border, a Grid or a StackPanel has none, and nothing would ever be heard) and must be a content element of
/// the tree (not a part of a control template). A region that is not in its window yet, because the view that holds it
/// is being built again, raises its events once the layout has placed it.
/// </para>
/// <para>
/// A <see cref="Window"/> is the region of the texts its views build again on every render (a line of «Probar», the
/// notice of a first tap): such a line is never the same element twice, so it can never be a live region. The window
/// never shows the text and never becomes a live region: it only raises the notification, on its own peer.
/// </para>
/// <para>
/// Announcing an empty text clears the region without raising anything: silence is never announced. Nothing here
/// moves the keyboard focus or the foreground (REG-01).
/// </para>
/// <para>
/// The notification strings go through <see cref="ForUiaBstr"/>, the workaround of a WPF defect that otherwise
/// delivers only half of each string to UI Automation clients (spike S3).
/// </para>
/// </remarks>
public sealed class LiveAnnouncer
{
    private const char Nul = (char)0;

    /// <summary>Activity id of every notification, so readers can group or replace Clícalo's notices.</summary>
    public const string ActivityId = "Clicalo.Notice";

    private (string Text, AnnouncementUrgency Urgency)? _waiting;

    /// <summary>
    /// Creates an announcer that speaks through <paramref name="region"/>, which is declared a polite live region
    /// at once (AVI-001) unless it already has a live setting or is a window. Call it on the region's UI thread.
    /// </summary>
    /// <param name="region">
    /// The element that shows the notice (the notice bar or status bar text); it becomes the live region. A window
    /// only says the texts of the regions its views build again.
    /// </param>
    public LiveAnnouncer(FrameworkElement region)
    {
        ArgumentNullException.ThrowIfNull(region);
        region.VerifyAccess();
        Debug.Assert(
            UIElementAutomationPeer.CreatePeerForElement(region) is not null,
            "A live region needs a UI Automation peer, and a "
                + region.GetType().Name
                + " has none."
        );
        Region = region;
        if (
            region is not Window
            && AutomationProperties.GetLiveSetting(region) == AutomationLiveSetting.Off
        )
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
    public void Announce(string text, AnnouncementUrgency urgency) =>
        Raise(text, urgency, show: true);

    /// <summary>
    /// Announces <paramref name="text"/> with <paramref name="urgency"/> without changing what the region shows: for a
    /// region whose own content already says what to do (the «Release all» pill), while the announcement says why it
    /// appeared.
    /// </summary>
    /// <param name="text">The localized text to say.</param>
    /// <param name="urgency">Polite or assertive.</param>
    public void Say(string text, AnnouncementUrgency urgency) => Raise(text, urgency, show: false);

    /// <summary>
    /// Works around a WPF defect found in spike S3: <c>AutomationPeer.RaiseNotificationEvent</c> hands its strings to
    /// <c>UiaRaiseNotificationEvent</c> as plain wide strings, but UI Automation reads them as <c>BSTR</c>. The
    /// length UI Automation reads is then the .NET length in characters taken as bytes, and readers receive only the
    /// first half of the text («Aviso 1» arrives as «Avi»). Appending as many NUL characters as the string has makes
    /// that length twice the number of characters, so exactly the original text arrives and the padding is never
    /// read. The Upstream test <c>WpfNotificationBstrTests</c> fails as soon as WPF passes real <c>BSTR</c>s; then this
    /// goes away.
    /// </summary>
    internal static string ForUiaBstr(string value) =>
        string.Concat(value, new string(Nul, value.Length));

    private void Raise(string text, AnnouncementUrgency urgency, bool show)
    {
        ArgumentNullException.ThrowIfNull(text);
        Region.VerifyAccess();
        var liveSetting = urgency switch
        {
            AnnouncementUrgency.Polite => AutomationLiveSetting.Polite,
            AnnouncementUrgency.Assertive => AutomationLiveSetting.Assertive,
            _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, message: null),
        };

        var window = Region is Window;
        if (!window)
        {
            AutomationProperties.SetLiveSetting(Region, liveSetting);
            if (show)
            {
                Show(text);
            }
        }

        LastText = text;
        LastUrgency = urgency;
        if (text.Length == 0)
        {
            _waiting = null;
            return;
        }

        Announcements++;
        if (PresentationSource.FromVisual(Region) is not null)
        {
            _waiting = null;
            RaiseEvents(text, urgency);
            return;
        }

        // Not in its window yet (the view is being built again): the events wait for the layout, and only the last
        // text waits.
        var first = _waiting is null;
        _waiting = (text, urgency);
        if (first)
        {
            _ = Region.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, RaiseWaiting);
        }
    }

    private void RaiseWaiting()
    {
        if (_waiting is not { } waiting)
        {
            return;
        }

        _waiting = null;
        if (PresentationSource.FromVisual(Region) is not null)
        {
            RaiseEvents(waiting.Text, waiting.Urgency);
        }
    }

    private void RaiseEvents(string text, AnnouncementUrgency urgency)
    {
        var peer =
            UIElementAutomationPeer.FromElement(Region)
            ?? UIElementAutomationPeer.CreatePeerForElement(Region);
        if (peer is null)
        {
            return;
        }

        if (Region is not Window)
        {
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        peer.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            urgency == AnnouncementUrgency.Assertive
                ? AutomationNotificationProcessing.ImportantAll
                : AutomationNotificationProcessing.MostRecent,
            ForUiaBstr(text),
            ForUiaBstr(ActivityId)
        );
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
