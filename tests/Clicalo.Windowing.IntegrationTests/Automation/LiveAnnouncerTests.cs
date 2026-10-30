using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// The announcer in process, headless: what the live region shows and declares for each urgency (ACC-001, UIA006).
/// That a UIA client receives <c>LiveRegionChanged</c> and the notification is <see cref="LiveRegionTests"/> (desktop).
/// </summary>
public sealed class LiveAnnouncerTests
{
    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-06")]
    public void A_polite_notice_is_shown_in_the_text_block_and_declared_polite() =>
        WpfThread.Invoke(() =>
        {
            var region = new TextBlock();
            var announcer = new LiveAnnouncer(region);

            announcer.Announce("Copiado", AnnouncementUrgency.Polite);

            region.Text.ShouldBe("Copiado");
            AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
            UIElementAutomationPeer.CreatePeerForElement(region).GetName().ShouldBe("Copiado");
            UIElementAutomationPeer
                .CreatePeerForElement(region)
                .GetLiveSetting()
                .ShouldBe(AutomationLiveSetting.Polite);
            announcer.LastText.ShouldBe("Copiado");
            announcer.LastUrgency.ShouldBe(AnnouncementUrgency.Polite);
            announcer.Announcements.ShouldBe(1);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "AVI-001")]
    public void The_region_is_a_polite_live_region_before_the_first_notice() =>
        WpfThread.Invoke(() =>
        {
            var plain = new TextBlock();
            var panic = new TextBlock();
            AutomationProperties.SetLiveSetting(panic, AutomationLiveSetting.Assertive);

            _ = new LiveAnnouncer(plain);
            _ = new LiveAnnouncer(panic);

            AutomationProperties.GetLiveSetting(plain).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetLiveSetting(panic).ShouldBe(AutomationLiveSetting.Assertive);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void An_assertive_notice_switches_the_region_to_assertive_and_back() =>
        WpfThread.Invoke(() =>
        {
            var region = new TextBlock();
            var announcer = new LiveAnnouncer(region);

            announcer.Announce("No pude volver a Word", AnnouncementUrgency.Assertive);
            AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Assertive);

            announcer.Announce("Listo", AnnouncementUrgency.Polite);
            AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
            announcer.Announcements.ShouldBe(2);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void A_content_control_shows_the_notice_as_content_and_name() =>
        WpfThread.Invoke(() =>
        {
            var region = new Label();
            new LiveAnnouncer(region).Announce("Pegado", AnnouncementUrgency.Polite);

            region.Content.ShouldBe("Pegado");
            AutomationProperties.GetName(region).ShouldBe("Pegado");
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void Any_other_element_receives_the_notice_as_its_automation_name() =>
        WpfThread.Invoke(() =>
        {
            // An element with a peer of its own: a Border, a Grid or a StackPanel has none, so it could never be heard.
            var region = new Image();
            new LiveAnnouncer(region).Announce("Soltado todo", AnnouncementUrgency.Assertive);

            AutomationProperties.GetName(region).ShouldBe("Soltado todo");
            AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Assertive);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void A_window_says_the_text_without_showing_it_or_becoming_a_live_region() =>
        WpfThread.Invoke(() =>
        {
            var content = new TextBlock { Text = "Probar" };
            var window = new Window { Content = content };
            try
            {
                var announcer = new LiveAnnouncer(window);

                announcer.Announce("¿Funcionó?", AnnouncementUrgency.Polite);
                announcer.Say("No se puede usar Ctrl+Alt+Supr", AnnouncementUrgency.Assertive);

                window.Content.ShouldBeSameAs(content, "a window never shows the text it says");
                AutomationProperties.GetName(window).ShouldBeEmpty();
                AutomationProperties.GetLiveSetting(window).ShouldBe(AutomationLiveSetting.Off);
                announcer.LastText.ShouldBe("No se puede usar Ctrl+Alt+Supr");
                announcer.LastUrgency.ShouldBe(AnnouncementUrgency.Assertive);
                announcer.Announcements.ShouldBe(2);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void A_region_that_is_not_in_a_window_yet_still_shows_and_counts_its_notices() =>
        WpfThread.Invoke(() =>
        {
            // A view being built again: its region is not in a window yet, so the events wait for the layout.
            var region = new TextBlock();
            var announcer = new LiveAnnouncer(region);

            announcer.Announce("Combinación bloqueada", AnnouncementUrgency.Assertive);
            announcer.Announce("Combinación especial", AnnouncementUrgency.Polite);
            WpfThread.DrainPendingWork();

            region.Text.ShouldBe("Combinación especial");
            announcer.Announcements.ShouldBe(2);
            announcer.LastUrgency.ShouldBe(AnnouncementUrgency.Polite);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void Clearing_the_region_is_not_an_announcement() =>
        WpfThread.Invoke(() =>
        {
            var region = new TextBlock();
            var announcer = new LiveAnnouncer(region);
            announcer.Announce("Copiado", AnnouncementUrgency.Polite);

            announcer.Announce(string.Empty, AnnouncementUrgency.Polite);

            region.Text.ShouldBeEmpty();
            announcer.LastText.ShouldBeEmpty();
            announcer.Announcements.ShouldBe(1);
        });

    [Fact]
    public void The_announcer_only_runs_on_the_thread_of_its_region()
    {
        var announcer = WpfThread.Invoke(() => new LiveAnnouncer(new TextBlock()));
        var foreignRegion = WpfThread.Invoke(() => new TextBlock());

        Should.Throw<InvalidOperationException>(() =>
            announcer.Announce("Copiado", AnnouncementUrgency.Polite)
        );
        Should.Throw<InvalidOperationException>(() => new LiveAnnouncer(foreignRegion));
    }

    [Fact]
    public void Bad_arguments_are_rejected() =>
        WpfThread.Invoke(() =>
        {
            var announcer = new LiveAnnouncer(new TextBlock());

            Should.Throw<ArgumentNullException>(() => new LiveAnnouncer(null!));
            Should.Throw<ArgumentNullException>(() =>
                announcer.Announce(null!, AnnouncementUrgency.Polite)
            );
            Should.Throw<ArgumentOutOfRangeException>(() =>
                announcer.Announce("Copiado", (AnnouncementUrgency)7)
            );
            LiveAnnouncer.ActivityId.ShouldBe("Clicalo.Notice");
        });
}
