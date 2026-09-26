using System.Windows.Automation.Peers;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Desktop;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Upstream;

/// <summary>
/// Watches a WPF defect that a Clícalo workaround compensates (blueprint §10.1, <c>Upstream/</c>): when a test here
/// fails, WPF has been fixed and the workaround must go.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class WpfNotificationBstrTests(UiaSurfaceFixture surface)
    : IClassFixture<UiaSurfaceFixture>
{
    private static readonly TimeSpan UpstreamSettle = TimeSpan.FromMilliseconds(300);

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
}
