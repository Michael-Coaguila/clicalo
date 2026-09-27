using Clicalo.Application.Ports;
using Clicalo.Application.Session;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Coordinators;

/// <summary>
/// Shows and hides the panel for the tray icon and a second start of Clícalo (BUR-003, SIS-003), on the Surfaces role
/// that owns <see cref="SessionStore"/>. Hiding with something held releases everything first: the engine receives
/// <see cref="EngineEvent.Terminal"/> with <see cref="TerminalReason.Hide"/> (SEG-007, blueprint §7.6), so no form of
/// the panel leaves a key down without a visible control to release it (REG-03).
/// </summary>
public sealed class PanelVisibilityCoordinator
{
    private readonly SessionStore _session;
    private readonly IEngineInbox _engine;

    /// <summary>Creates the coordinator.</summary>
    /// <param name="session">The session of the Surfaces role.</param>
    /// <param name="engine">The engine mailbox.</param>
    public PanelVisibilityCoordinator(SessionStore session, IEngineInbox engine)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(engine);
        _session = session;
        _engine = engine;
    }

    /// <summary>Whether the panel is on screen.</summary>
    public bool IsVisible => _session.Current.Presence == PanelPresence.Visible;

    /// <summary>A click on the tray icon: shows the panel when hidden and hides it when visible (BUR-003).</summary>
    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>Shows the panel (tray, or a second start of Clícalo: SIS-003).</summary>
    public void Show() => _ = _session.Dispatch(new SessionAction.Show());

    /// <summary>Hides the panel and releases everything held (BUR-003, SEG-007).</summary>
    public void Hide()
    {
        if (_session.Dispatch(new SessionAction.Hide()))
        {
            _ = _engine.Post(new EngineEvent.Terminal(TerminalReason.Hide));
        }
    }
}
