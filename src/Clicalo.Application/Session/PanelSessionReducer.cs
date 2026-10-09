namespace Clicalo.Application.Session;

/// <summary>
/// The only way a <see cref="PanelSession"/> changes (blueprint §6.4): a pure function covered by a transition table.
/// An action that changes nothing returns the same instance, so observers can tell a real change by reference.
/// </summary>
public static class PanelSessionReducer
{
    /// <summary>Applies <paramref name="action"/> to <paramref name="session"/>.</summary>
    /// <param name="session">The current session.</param>
    /// <param name="action">The intention.</param>
    /// <returns>The next session, or <paramref name="session"/> itself when nothing changes.</returns>
    public static PanelSession Reduce(PanelSession session, SessionAction action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            SessionAction.Show => WithPresence(session, PanelPresence.Visible),
            SessionAction.Hide => WithPresence(session, PanelPresence.Hidden),
            SessionAction.ToggleVisibility => WithPresence(
                session,
                session.Presence == PanelPresence.Visible
                    ? PanelPresence.Hidden
                    : PanelPresence.Visible
            ),
            SessionAction.ShowProfile show when show.Profile != session.View => session with
            {
                View = show.Profile,
                PickerOpen = false,
                Version = session.Version + 1,
            },
            SessionAction.ShowProfile => session,
            SessionAction.TogglePicker => WithPicker(
                session,
                open: !session.PickerOpen && session.Presence == PanelPresence.Visible
            ),
            SessionAction.ClosePicker => WithPicker(session, open: false),
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                action.GetType().Name,
                "Unknown session action."
            ),
        };
    }

    private static PanelSession WithPicker(PanelSession session, bool open) =>
        session.PickerOpen == open
            ? session
            : session with
            {
                PickerOpen = open,
                Version = session.Version + 1,
            };

    private static PanelSession WithPresence(PanelSession session, PanelPresence presence) =>
        session.Presence == presence
            ? session
            : session with
            {
                Presence = presence,
                // A hidden panel keeps no layer open (SEL-002).
                PickerOpen = session.PickerOpen && presence == PanelPresence.Visible,
                Version = session.Version + 1,
            };
}
