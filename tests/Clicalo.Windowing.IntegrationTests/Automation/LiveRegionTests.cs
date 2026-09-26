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

    [DesktopFact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "AVI-001")]
    public async Task A_notice_raises_LiveRegionChanged_and_a_notification()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var notice = surface.Element(TileLab.NoticeId);
        var liveChanges = new EventLog<int>();
        var notifications = new EventLog<Received>();
        using var live = notice.RegisterAutomationEvent(
            surface.Automation.EventLibrary.Element.LiveRegionChangedEvent,
            TreeScope.Element,
            (_, _) => liveChanges.Record(Environment.CurrentManagedThreadId)
        );
        using var notification = notice.RegisterNotificationEvent(
            TreeScope.Element,
            (_, kind, processing, text, activityId) =>
                notifications.Record(
                    new Received(kind, processing, text ?? string.Empty, activityId ?? string.Empty)
                )
        );
        await UiaTreeTests.WaitForListenerAsync(
            AutomationEvents.LiveRegionChanged,
            AutomationEvents.Notification
        );

        var violations = new List<UiaViolation>();
        var expected = new List<Received>();
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
            var announced = new Received(
                NotificationKind.Other,
                Processing(urgency),
                text,
                LiveAnnouncer.ActivityId
            );
            expected.Add(announced);
            await WaitForAsync(notifications, notificationStart, announced);
            violations.AddRange(
                await surface.ForegroundViolationsAsync(
                    cursor,
                    string.Create(CultureInfo.InvariantCulture, $"{urgency} notice #{cycle}")
                )
            );

            notice.Name.ShouldBe(text);
            notice.Properties.LiveSetting.Value.ShouldBe(
                urgency == AnnouncementUrgency.Assertive
                    ? LiveSetting.Assertive
                    : LiveSetting.Polite
            );
        }

        violations.ShouldBeEmpty();

        // A client in the same process may get each notification twice, even after the next one; every copy must
        // be exactly one of the notices announced (a cut text or a wrong urgency is not).
        notifications.Since(0).ShouldAllBe(received => expected.Contains(received));
    }

    /// <summary>Waits until <paramref name="notice"/> arrives after <paramref name="start"/>, whatever else arrives.</summary>
    private static async Task WaitForAsync(EventLog<Received> log, int start, Received notice)
    {
        while (true)
        {
            var seen = log.Count;
            if (log.Since(start).Contains(notice))
            {
                return;
            }

            await log.WaitForAsync(
                seen,
                1,
                UiaSurfaceFixture.EventTimeout,
                TestContext.Current.CancellationToken
            );
        }
    }

    private static NotificationProcessing Processing(AnnouncementUrgency urgency) =>
        urgency == AnnouncementUrgency.Assertive
            ? NotificationProcessing.ImportantAll
            : NotificationProcessing.MostRecent;

    /// <summary>A notification as a UI Automation client receives it.</summary>
    private readonly record struct Received(
        NotificationKind Kind,
        NotificationProcessing Processing,
        string Text,
        string ActivityId
    );
}
