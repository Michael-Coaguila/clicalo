using Clicalo.Application.Coordinators;
using Clicalo.Application.Session;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>
/// Showing and hiding the panel from the tray or a second start (BUR-003, SIS-003): hiding releases everything, so no
/// form of the panel leaves a key down without a visible control to release it (REG-03, SEG-007).
/// </summary>
/// <remarks>Each test creates the store on its own thread: xUnit may construct the class on another one.</remarks>
[Trait("Req", "BUR-003")]
public sealed class PanelVisibilityCoordinatorTests
{
    private readonly RecordingEngineInbox _engine = new();
    private SessionStore _session = null!;
    private PanelVisibilityCoordinator _coordinator = null!;

    private void Start()
    {
        _session = new SessionStore(PanelSession.Initial(ProfileId.General));
        _coordinator = new PanelVisibilityCoordinator(_session, _engine);
    }

    [Fact]
    [Trait("Req", "SEG-007")]
    [Trait("Req", "REG-03")]
    public void Hiding_from_the_tray_releases_everything_with_a_terminal_event()
    {
        Start();
        _coordinator.Toggle();

        _session.Current.Presence.ShouldBe(PanelPresence.Hidden);
        _coordinator.IsVisible.ShouldBeFalse();
        var terminal = _engine.Events.ShouldHaveSingleItem().ShouldBeOfType<EngineEvent.Terminal>();
        terminal.Reason.ShouldBe(TerminalReason.Hide);
        terminal.Lane.ShouldBe(EngineLane.Priority);
    }

    [Fact]
    public void Showing_again_sends_nothing_to_the_engine()
    {
        Start();
        _coordinator.Hide();
        var afterHide = _engine.Events.Count;

        _coordinator.Toggle();

        _session.Current.Presence.ShouldBe(PanelPresence.Visible);
        _engine.Events.Count.ShouldBe(afterHide);
    }

    [Fact]
    [Trait("Req", "SIS-003")]
    public void A_second_start_shows_the_panel_that_is_already_there()
    {
        Start();
        _coordinator.Hide();

        _coordinator.Show();
        _coordinator.Show();

        _session.Current.Presence.ShouldBe(PanelPresence.Visible);
        _session.Current.Version.ShouldBe(2, "hide and one show; the second show changed nothing");
    }

    [Fact]
    public void Hiding_a_hidden_panel_releases_nothing_again()
    {
        Start();
        _coordinator.Hide();
        _coordinator.Hide();

        _engine.Events.Count.ShouldBe(1);
    }
}
