using Clicalo.Application.Session;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Session;

/// <summary>
/// The transition table of the M2 panel session (blueprint §6.4): every action from every presence, and nothing
/// changes (same instance, same version) when the action has no effect.
/// </summary>
[Trait("Req", "PAN-001")]
public sealed class PanelSessionReducerTests
{
    private static readonly ProfileId Word = new("p-word");

    public static TheoryData<PanelPresence, string, PanelPresence> PresenceTable =>
        new()
        {
            { PanelPresence.Visible, nameof(SessionAction.Show), PanelPresence.Visible },
            { PanelPresence.Visible, nameof(SessionAction.Hide), PanelPresence.Hidden },
            { PanelPresence.Visible, nameof(SessionAction.ToggleVisibility), PanelPresence.Hidden },
            { PanelPresence.Hidden, nameof(SessionAction.Show), PanelPresence.Visible },
            { PanelPresence.Hidden, nameof(SessionAction.Hide), PanelPresence.Hidden },
            { PanelPresence.Hidden, nameof(SessionAction.ToggleVisibility), PanelPresence.Visible },
        };

    [Theory]
    [MemberData(nameof(PresenceTable))]
    [Trait("Req", "BUR-003")]
    public void Presence_follows_the_table(PanelPresence from, string action, PanelPresence to)
    {
        var session = new PanelSession(from, ProfileId.General, 5);

        var next = PanelSessionReducer.Reduce(session, Action(action));

        next.Presence.ShouldBe(to);
        if (from == to)
        {
            next.ShouldBeSameAs(session, "an action without effect returns the same session");
        }
        else
        {
            next.Version.ShouldBe(6);
            next.View.ShouldBe(ProfileId.General);
        }
    }

    [Fact]
    [Trait("Req", "PER-001")]
    public void Showing_another_profile_changes_the_view_and_the_version()
    {
        var session = PanelSession.Initial(ProfileId.General);

        var next = PanelSessionReducer.Reduce(session, new SessionAction.ShowProfile(Word));

        next.View.ShouldBe(Word);
        next.Version.ShouldBe(1);
        next.Presence.ShouldBe(PanelPresence.Visible);
        PanelSessionReducer
            .Reduce(next, new SessionAction.ShowProfile(Word))
            .ShouldBeSameAs(next, "the same profile again changes nothing");
    }

    [Fact]
    [Trait("Req", "SEL-002")]
    public void The_profile_button_opens_and_closes_the_profile_grid()
    {
        var session = PanelSession.Initial(ProfileId.General);

        var open = PanelSessionReducer.Reduce(session, new SessionAction.TogglePicker());
        var closed = PanelSessionReducer.Reduce(open, new SessionAction.TogglePicker());

        open.PickerOpen.ShouldBeTrue();
        open.Version.ShouldBe(1);
        closed.PickerOpen.ShouldBeFalse();
        closed.Version.ShouldBe(2);
        PanelSessionReducer
            .Reduce(closed, new SessionAction.ClosePicker())
            .ShouldBeSameAs(closed, "closing a closed grid changes nothing");
    }

    [Fact]
    [Trait("Req", "SEL-004")]
    public void Choosing_a_profile_or_hiding_the_panel_closes_the_profile_grid()
    {
        var open = PanelSessionReducer.Reduce(
            PanelSession.Initial(ProfileId.General),
            new SessionAction.TogglePicker()
        );

        PanelSessionReducer
            .Reduce(open, new SessionAction.ShowProfile(Word))
            .PickerOpen.ShouldBeFalse();
        PanelSessionReducer.Reduce(open, new SessionAction.Hide()).PickerOpen.ShouldBeFalse();
        PanelSessionReducer
            .Reduce(open, new SessionAction.ToggleVisibility())
            .PickerOpen.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "SEL-002")]
    public void A_hidden_panel_does_not_open_the_profile_grid()
    {
        var hidden = new PanelSession(PanelPresence.Hidden, ProfileId.General, 3);

        PanelSessionReducer.Reduce(hidden, new SessionAction.TogglePicker()).ShouldBeSameAs(hidden);
    }

    [Fact]
    [Trait("Req", "NFR-001")]
    public void A_new_process_starts_with_the_panel_visible()
    {
        var session = PanelSession.Initial(Word);

        session.ShouldBe(new PanelSession(PanelPresence.Visible, Word, 0));
    }

    private static SessionAction Action(string name) =>
        name switch
        {
            nameof(SessionAction.Show) => new SessionAction.Show(),
            nameof(SessionAction.Hide) => new SessionAction.Hide(),
            nameof(SessionAction.ToggleVisibility) => new SessionAction.ToggleVisibility(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
}
