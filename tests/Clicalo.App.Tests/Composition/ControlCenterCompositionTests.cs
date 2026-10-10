using Clicalo.App.Composition;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.App.Tests.Composition;

/// <summary>
/// The panel and the Control Center tied as the app ties them (<see cref="PanelLinks.Connect"/>), without the
/// executable: a notice of the panel reaches the status bar (CCM-003) and «Probar ahora» fixes [switching] in the panel
/// until it ends (PRB-004). The window of the Control Center is built and never shown.
/// </summary>
public sealed class ControlCenterCompositionTests
{
    [Fact]
    [Trait("Req", "CCM-003")]
    [Trait("Req", "AVI-002")]
    public void A_notice_of_the_panel_reaches_the_status_bar_and_leaves_it_when_it_ends() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            using var theme = new ThemeService(new CompositionWorld.DarkSystem());
            using var controlCenter = world.ControlCenter(theme);
            PanelLinks.Connect(world.Panel, controlCenter, world.Interaction);
            var viewModel = controlCenter.Prepare();
            var atRest = viewModel.Status.Text;

            world.Panel.Notify(L.Deleted, NoticeTone.Warning, "delete");
            UiThread.Drain();

            viewModel.Status.Text.ShouldBe(world.Text(L.Deleted));
            viewModel.Status.Icon.ShouldBe("delete");
            viewModel.Status.IsWarning.ShouldBeTrue();

            world.Time.Advance(Timings.Notices.NoticeDuration);
            UiThread.Drain();

            world.Panel.CurrentNotice.ShouldBeNull();
            viewModel.Status.Text.ShouldBe(atRest, "the bar rests with the panel");
        });

    [Fact]
    [Trait("Req", "CCM-003")]
    public void A_notice_already_on_show_is_in_the_status_bar_when_the_window_is_built() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            using var theme = new ThemeService(new CompositionWorld.DarkSystem());
            using var controlCenter = world.ControlCenter(theme);
            world.Panel.Notify(L.Deleted, NoticeTone.Notice, "delete");
            UiThread.Drain();

            PanelLinks.Connect(world.Panel, controlCenter, world.Interaction);
            var viewModel = controlCenter.Prepare();

            viewModel.Status.Text.ShouldBe(world.Text(L.Deleted));
        });

    [Fact]
    [Trait("Req", "PRB-004")]
    public void Try_now_fixes_switching_in_the_panel_and_removes_it_when_it_ends() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            using var theme = new ThemeService(new CompositionWorld.DarkSystem());
            using var controlCenter = world.ControlCenter(theme);
            PanelLinks.Connect(world.Panel, controlCenter, world.Interaction);
            var viewModel = controlCenter.Prepare();
            var shown = new List<string?>();
            world.Panel.NoticePublished += (_, published) =>
                shown.Add(published.Notice is { } notice ? world.Text(notice.Text) : null);
            var switching = world.Text(L.Switching(app: "Bloc de notas"));
            var seenInTheBar = false;
            viewModel.PropertyChanged += (_, _) =>
                seenInTheBar |= string.Equals(
                    viewModel.Status.Text,
                    switching,
                    StringComparison.Ordinal
                );

            // An elevated target and a shortcut with nothing to send: the try ends before it touches any window.
            var outcome = UiThread.Wait(
                controlCenter
                    .TryNowAsync(
                        CompositionWorld.Shortcut("try", "Prueba"),
                        new OpenApp(
                            new ProcessName("notepad.exe"),
                            "Bloc de notas",
                            new WindowToken(1),
                            Elevated: true,
                            ExecutablePath: null
                        ),
                        CancellationToken.None
                    )
                    .AsTask()
            );
            UiThread.Drain();

            outcome.ShouldBeOneOf(TryNowOutcome.Incomplete, TryNowOutcome.Elevated);
            shown.ShouldBe([switching, null], "fixed while it runs, gone when it ends");
            seenInTheBar.ShouldBeTrue("the status bar shows what the panel shows (CCM-003)");
            world.Panel.CurrentNotice.ShouldBeNull();
            world.Foreground.Requests.ShouldBe(0);

            // Fixed: time alone does not take it away while the try runs (AVI-002).
            controlCenter.PanelNotice.ShouldNotBeNull()(L.Switching(app: "Bloc de notas"));
            world.Time.Advance(Timings.Notices.NoticeDuration * 4);
            UiThread.Drain();
            world.Text(world.Panel.CurrentNotice.ShouldNotBeNull().Text).ShouldBe(switching);
            controlCenter.PanelNotice(null);
            UiThread.Drain();
            world.Panel.CurrentNotice.ShouldBeNull();
        });
}
