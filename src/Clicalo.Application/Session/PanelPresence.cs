namespace Clicalo.Application.Session;

/// <summary>
/// Whether the panel is on screen (blueprint §6.4, <c>PanelSession.Presence</c>). M2 has the full panel and the hidden
/// state only; the Tab, the bubble and the compact form join in M3 (PAN-001).
/// </summary>
public enum PanelPresence
{
    /// <summary>The panel is shown (passively: it never takes the foreground, REG-01).</summary>
    Visible,

    /// <summary>The panel is hidden; the tray icon brings it back (BUR-003, BUR-005).</summary>
    Hidden,
}
