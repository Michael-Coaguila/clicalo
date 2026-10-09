using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// The engine's notices (keys of <c>data/i18n</c>).
/// </summary>
internal static class EngineNotices
{
    /// <summary>Everything was released (SEG-003).</summary>
    public static Message ReleasedAll => L.ReleasedAll;

    /// <summary>A real app switch released everything (SEG-005).</summary>
    public static Message ReleasedOnSwitch => L.ReleasedSwitch;

    /// <summary>A held item reached its automatic release limit (SEG-004).</summary>
    /// <param name="seconds">The limit in seconds.</param>
    public static Message ReleasedAutomatically(long seconds) => L.ReleasedAuto(seconds);

    /// <summary>Everything was released because the session was locked or the machine suspended (SEG-006).</summary>
    public static Message ReleasedOnLock => L.ReleasedOnLock;

    /// <summary>The engine failed and released everything (NFR-005).</summary>
    public static Message Fault => L.EngineFault;

    /// <summary>A tap on a shortcut that is incomplete (EJE-015).</summary>
    public static Message Incomplete => L.IncompleteTap;

    /// <summary>
    /// A key of the shortcut does not exist in the layout of the app in front (EC-EJE-10); the key is not named until
    /// the resolver reports which one.
    /// </summary>
    public static Message NotInLayout => L.Incomplete;

    /// <summary>The combination is blocked in the panel (EJE-014).</summary>
    public static Message Blocked => L.BlockedB;

    /// <summary>The foreground app runs as administrator and Clícalo does not (EJE-013).</summary>
    /// <param name="app">The app's process name.</param>
    public static Message Elevated(string app) => L.AdminMsg(app);

    /// <summary>A tap was not sent because the app in front runs as administrator (EJE-013).</summary>
    /// <param name="app">The app's process name.</param>
    public static Message ElevatedRefused(string app) => L.ElevatedRefused(app);

    /// <summary>The first tap armed a shortcut that asks for confirmation (EJE-002).</summary>
    public static Message ConfirmArmed => L.ConfirmClose;

    /// <summary>A Hold was released (EJE-004).</summary>
    public static Message Released => L.Released;

    /// <summary>A Toggle was released (EJE-007).</summary>
    public static Message Unlatched => L.Unlatched;

    /// <summary>A sticky modifier joins the next tap or click (FIJ-005).</summary>
    public static Message StickyOnce => L.ModOnce;

    /// <summary>A sticky modifier is locked (FIJ-005).</summary>
    public static Message StickyLocked => L.ModLock;

    /// <summary>A sticky modifier was released (FIJ-005).</summary>
    public static Message StickyOff => L.ModOff;
}
