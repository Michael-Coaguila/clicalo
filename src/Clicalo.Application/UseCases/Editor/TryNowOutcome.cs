namespace Clicalo.Application.UseCases.Editor;

/// <summary>How «Probar ahora» ended (PRB-004, PRB-007).</summary>
public enum TryNowOutcome
{
    /// <summary>The action was sent and the Control Center is back in front with «¿Hizo lo esperado?».</summary>
    Asked,

    /// <summary>
    /// The action was sent but Windows did not give the foreground back: the Control Center flashes in the taskbar and
    /// the question waits (PRB-007).
    /// </summary>
    AskedFlashed,

    /// <summary>The app could not be brought to the front (it closed, or Windows refused): nothing was sent.</summary>
    NotActivated,

    /// <summary>The app runs as administrator and Clícalo does not: nothing was sent, the admin notice shows (PRB-007).</summary>
    Elevated,

    /// <summary>A blocked combination is never tried (PRB-004).</summary>
    Blocked,

    /// <summary>The shortcut is incomplete: there is nothing to send (EJE-015).</summary>
    Incomplete,

    /// <summary>The engine is stopping.</summary>
    EngineStopped,

    /// <summary>
    /// The try was cancelled before it ended: a key it held or latched was released (REG-03) and the Control Center came
    /// back; nothing is asked.
    /// </summary>
    Cancelled,
}
