namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The one form the panel has at a time (PAN-001): Full, Compact, the Tab with its handle closed or its bar open, the
/// bubble, or hidden.
/// </summary>
public enum PanelForm
{
    /// <summary>Hidden from the tray: no surface shows (BUR-003).</summary>
    Hidden,

    /// <summary>The Full view.</summary>
    Full,

    /// <summary>The Compact view (VCO-001).</summary>
    Compact,

    /// <summary>The Tab view with its handle closed (PES-001).</summary>
    DockClosed,

    /// <summary>The Tab view with its bar open (PES-005).</summary>
    DockOpen,

    /// <summary>The 64 px bubble (BUR-001).</summary>
    Bubble,
}
