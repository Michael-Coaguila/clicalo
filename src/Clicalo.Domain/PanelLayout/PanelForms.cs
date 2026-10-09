using Clicalo.Domain.Settings;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// Which form the panel takes (PAN-001), from the view of General (<c>density</c>), whether the panel is shown, minimized
/// to the bubble or has its bar open. Pure; a table of transitions covers it.
/// </summary>
public static class PanelForms
{
    /// <summary>The form of the panel.</summary>
    /// <param name="visible">The panel is shown (not hidden from the tray).</param>
    /// <param name="density">The view: Full, Compact or Tab.</param>
    /// <param name="minimized">«−» turned it into the bubble; the Tab view has no «−» and never minimizes.</param>
    /// <param name="dockOpen">The bar of the Tab view is open.</param>
    public static PanelForm Of(bool visible, PanelDensity density, bool minimized, bool dockOpen) =>
        !visible ? PanelForm.Hidden
        : density == PanelDensity.Dock
            ? dockOpen ? PanelForm.DockOpen
                : PanelForm.DockClosed
        : minimized ? PanelForm.Bubble
        : density == PanelDensity.Compact ? PanelForm.Compact
        : PanelForm.Full;

    /// <summary>Whether the form is the panel window (Full or Compact).</summary>
    /// <param name="form">The form.</param>
    public static bool IsPanel(PanelForm form) => form is PanelForm.Full or PanelForm.Compact;

    /// <summary>Whether the form is the Tab view, closed or open.</summary>
    /// <param name="form">The form.</param>
    public static bool IsDock(PanelForm form) => form is PanelForm.DockClosed or PanelForm.DockOpen;
}
