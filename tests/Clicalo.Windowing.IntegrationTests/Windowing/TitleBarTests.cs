using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Shell;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Controls;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The title bar of the Control Center (D13, CCM-001): a real Windows window with minimize, maximize or restore and
/// close in zones of 56 × 56, named for UI Automation and never by their glyph, over the chrome of
/// <see cref="WindowChrome"/>. Built without showing any window; the live checks (Snap, WindowPattern, 175 %) are in
/// the manual acceptance script.
/// </summary>
public sealed class TitleBarTests
{
    [Fact]
    [Trait("Req", "CCM-001")]
    [Trait("Req", "REG-06")]
    public void The_three_buttons_are_named_invokable_zones_of_56() =>
        WpfThread.Invoke(() =>
        {
            var bar = new TitleBar();
            bar.SetNames("Minimizar", "Maximizar", "Restaurar tamaño", "Cerrar");
            using var service = BaseControlTests.Host(bar);

            foreach (
                var (button, name) in new[]
                {
                    (bar.Minimize, "Minimizar"),
                    (bar.Maximize, "Maximizar"),
                    (bar.Close, "Cerrar"),
                }
            )
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(button);
                peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
                peer.GetName().ShouldBe(name);
                peer.GetPattern(PatternInterface.Invoke).ShouldNotBeNull();
                peer.GetChildren().ShouldBeNull("the glyph is never a name");
                button.ActualWidth.ShouldBe(ComponentSize.CaptionButton);
                button.ActualHeight.ShouldBe(ComponentSize.CaptionButton);
            }

            bar.ActualHeight.ShouldBe(ComponentSize.TitleBar);
        });

    [Fact]
    [Trait("Req", "CCM-001")]
    public void Attaching_gives_the_window_its_chrome_and_close_runs_what_the_view_says() =>
        WpfThread.Invoke(() =>
        {
            var bar = new TitleBar();
            var window = new Window { Content = bar, ShowActivated = false };
            var closed = 0;
            bar.Attach(window, () => closed++);
            bar.SetNames("Minimizar", "Maximizar", "Restaurar tamaño", "Cerrar");

            var chrome = WindowChrome.GetWindowChrome(window).ShouldNotBeNull();
            chrome.CaptionHeight.ShouldBe(ComponentSize.TitleBar - Stroke.Line);
            chrome.ResizeBorderThickness.ShouldBe(new Thickness(Space.S2));
            WindowChrome.GetIsHitTestVisibleInChrome(bar.Maximize).ShouldBeTrue();
            Should.Throw<InvalidOperationException>(() => bar.Attach(window, () => { }));

            (
                (IInvokeProvider)
                    UIElementAutomationPeer
                        .CreatePeerForElement(bar.Close)
                        .GetPattern(PatternInterface.Invoke)
            ).Invoke();
            WpfThread.DrainPendingWork();
            closed.ShouldBe(1);
            AutomationProperties.GetName(bar.Maximize).ShouldBe("Maximizar");
            window.Close();
        });
}
