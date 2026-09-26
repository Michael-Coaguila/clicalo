using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// The engine's notices, built from existing keys of <c>data/i18n</c> (only the app package adds keys in M2; the new
/// texts of EJE-010, EJE-013, EJE-015, SEG-006 and NFR-005 are requested in the package report).
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

    /// <summary>The shortcut is incomplete, or one of its keys does not exist in the layout (EJE-015, EC-EJE-10).</summary>
    public static Message Incomplete => L.Incomplete;

    /// <summary>The combination is blocked in the panel (EJE-014).</summary>
    public static Message Blocked => L.BlockedB;

    /// <summary>The foreground app runs as administrator and Clícalo does not (EJE-013).</summary>
    /// <param name="app">The app's process name.</param>
    public static Message Elevated(string app) => L.AdminMsg(app);

    /// <summary>The first tap armed a shortcut that asks for confirmation (EJE-002).</summary>
    public static Message ConfirmArmed => L.ConfirmClose;

    /// <summary>A Hold pressed its keys (EJE-004).</summary>
    public static Message Holding => L.Holding;

    /// <summary>A Hold was released (EJE-004).</summary>
    public static Message Released => L.Released;

    /// <summary>A Toggle latched (EJE-007).</summary>
    public static Message Latched => L.Latched;

    /// <summary>A Toggle was released (EJE-007).</summary>
    public static Message Unlatched => L.Unlatched;

    /// <summary>A macro ran to the end (EJE-010).</summary>
    public static Message MacroRan => L.RanMacro;

    /// <summary>A web address is opening (EJE-011).</summary>
    public static Message Opened => L.Opened;
}
