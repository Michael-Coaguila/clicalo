using System.Globalization;
using System.Windows.Automation.Peers;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using Clicalo.Windowing.IntegrationTests.Desktop;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// S3 · UIA006: a notice announced on the non-activatable surface reaches a UI Automation client twice, as the
/// <c>LiveRegionChanged</c> event of the notice bar and as a notification with <c>ActivityId = "Clicalo.Notice"</c>,
/// with its text and its urgency, without touching the foreground. 20 cycles, alternating polite and assertive.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "ACC-001")]
[Trait("Req", "REG-06")]
public sealed class LiveRegionTests(UiaSurfaceFixture surface) : IClassFixture<UiaSurfaceFixture>
{
    private const int Cycles = 20;

    private static readonly TimeSpan UpstreamSettle = TimeSpan.FromMilliseconds(300);

    [DesktopFact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "AVI-001")]
    public async Task A_notice_raises_LiveRegionChanged_and_a_notification()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var notice = surface.Element(TileLab.NoticeId);
        var liveChanges = new EventLog<int>();
        var notifications =
            new EventLog<(
                NotificationKind Kind,
                NotificationProcessing Processing,
                string Text,
                string ActivityId
            )>();
        using var live = notice.RegisterAutomationEvent(
            surface.Automation.EventLibrary.Element.LiveRegionChangedEvent,
            TreeScope.Element,
            (_, _) => liveChanges.Record(Environment.CurrentManagedThreadId)
        );
        using var notification = notice.RegisterNotificationEvent(
            TreeScope.Element,
            (_, kind, processing, text, activityId) =>
                notifications.Record(
                    (kind, processing, text ?? string.Empty, activityId ?? string.Empty)
                )
        );
        await UiaTreeTests.WaitForListenerAsync(
            AutomationEvents.LiveRegionChanged,
            AutomationEvents.Notification
        );

        var violations = new List<UiaViolation>();
        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            var urgency =
                cycle % 2 == 0 ? AnnouncementUrgency.Assertive : AnnouncementUrgency.Polite;
            var text = string.Create(CultureInfo.InvariantCulture, $"Aviso {cycle}");
            var cursor = await surface.PrepareAsync();
            var liveStart = liveChanges.Count;
            var notificationStart = notifications.Count;

            WpfThread.Invoke(() => surface.Lab.Announcer.Announce(text, urgency));

            await liveChanges.WaitForAsync(
                liveStart,
                1,
                UiaSurfaceFixture.EventTimeout,
                TestContext.Current.CancellationToken
            );
            await notifications.WaitForAsync(
                notificationStart,
                1,
                UiaSurfaceFixture.EventTimeout,
                TestContext.Current.CancellationToken
            );
            violations.AddRange(
                await surface.ForegroundViolationsAsync(
                    cursor,
                    string.Create(CultureInfo.InvariantCulture, $"{urgency} notice #{cycle}")
                )
            );

            // A client in the same process may get each event twice; every copy must be right.
            notifications
                .Since(notificationStart)
                .ShouldAllBe(received =>
                    received.Kind == NotificationKind.Other
                    && received.Processing == Processing(urgency)
                    && received.Text == text
                    && received.ActivityId == LiveAnnouncer.ActivityId
                );
            notice.Name.ShouldBe(text);
            notice.Properties.LiveSetting.Value.ShouldBe(
                urgency == AnnouncementUrgency.Assertive
                    ? LiveSetting.Assertive
                    : LiveSetting.Polite
            );
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// The WPF defect behind <c>LiveAnnouncer.ForUiaBstr</c>: a plain string given to
    /// <c>AutomationPeer.RaiseNotificationEvent</c> reaches UI Automation clients cut in half, because WPF passes a
    /// wide string where UI Automation reads a <c>BSTR</c> (found in spike S3). When this fails, WPF has been fixed:
    /// remove the padding.
    /// </summary>
    [DesktopFact]
    [Trait("Upstream", "wpf-notification-bstr")]
    public async Task WPF_delivers_half_of_a_plain_notification_string()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        await surface.PrepareAsync();
        var notice = surface.Element(TileLab.NoticeId);
        var texts = new EventLog<string>();
        using var notification = notice.RegisterNotificationEvent(
            TreeScope.Element,
            (_, _, _, text, _) => texts.Record(text ?? string.Empty)
        );
        await UiaTreeTests.WaitForListenerAsync(AutomationEvents.Notification);

        WpfThread.Invoke(() =>
            UIElementAutomationPeer
                .CreatePeerForElement(surface.Lab.Notice)
                .RaiseNotificationEvent(
                    System.Windows.Automation.AutomationNotificationKind.Other,
                    System.Windows.Automation.AutomationNotificationProcessing.All,
                    "Soltado todo",
                    "Clicalo.Upstream"
                )
        );

        await texts.WaitForAsync(
            0,
            1,
            UiaSurfaceFixture.EventTimeout,
            TestContext.Current.CancellationToken
        );
        await Task.Delay(UpstreamSettle, TestContext.Current.CancellationToken);

        // 12 characters read as a BSTR of 12 bytes: 6 characters. (An in-process client may also get an intact copy.)
        texts
            .Since(0)
            .ShouldContain(text => string.Equals(text, "Soltad", StringComparison.Ordinal));
    }

    private static NotificationProcessing Processing(AnnouncementUrgency urgency) =>
        urgency == AnnouncementUrgency.Assertive
            ? NotificationProcessing.ImportantAll
            : NotificationProcessing.MostRecent;
}
