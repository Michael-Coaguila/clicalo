namespace Clicalo.Domain.PanelLayout;

/// <summary>The window beside the open bar of the Tab view (PES-010, PES-011, AUD-13): at most one at a time.</summary>
public enum DockFlyout
{
    /// <summary>No window beside the bar.</summary>
    None,

    /// <summary>📌 «Pinned»: the shortcuts of Always visible (PES-010).</summary>
    Pinned,

    /// <summary>The profile grid (PES-011).</summary>
    Profiles,

    /// <summary>Sticky keys: Ctrl, Alt, Shift and Win (PES-008, AUD-13).</summary>
    Sticky,
}
