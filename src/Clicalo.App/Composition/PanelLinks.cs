using Clicalo.Application.Coordinators;
using Clicalo.Application.Interaction;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Presentation.Panel;

namespace Clicalo.App.Composition;

/// <summary>
/// What ties the panel to the tray and to the Control Center besides their windows: kept apart from the start of the
/// app so the composition is tested without launching it. Everything here runs on the UI thread.
/// </summary>
internal static class PanelLinks
{
    private const string SwitchingIcon = "swap_horiz";

    /// <summary>
    /// A click on the tray icon and the global shortcut (BUR-003, BUR-005): with the bubble on screen the panel comes
    /// back; otherwise it shows or hides. Only the session and the interaction change: nothing asks for the foreground
    /// (REG-01).
    /// </summary>
    /// <param name="visibility">Shows and hides the panel.</param>
    /// <param name="interaction">Whether the panel is minimized to the bubble.</param>
    public static void ToggleFromTray(
        PanelVisibilityCoordinator visibility,
        InteractionStore interaction
    )
    {
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(interaction);
        if (visibility.IsVisible && interaction.Current.Minimized)
        {
            _ = interaction.Dispatch(new InteractionAction.Restore());
        }
        else
        {
            visibility.Toggle();
        }
    }

    /// <summary>Ties the panel and the Control Center to each other.</summary>
    /// <param name="panel">The panel.</param>
    /// <param name="controlCenter">The Control Center.</param>
    /// <param name="interaction">The interaction state of the Surfaces role.</param>
    public static void Connect(
        PanelComposer panel,
        ControlCenterComposer controlCenter,
        InteractionStore interaction
    )
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(controlCenter);
        ArgumentNullException.ThrowIfNull(interaction);
        panel.ControlCenter = controlCenter;

        // CCM-004: nothing dims while the control center is open; ATJ-008: the capture notice shows in the panel.
        controlCenter.StateChanged += (_, _) =>
        {
            _ = interaction.Dispatch(
                new InteractionAction.SetOpen(DimExceptions.ControlCenterOpen, controlCenter.IsOpen)
            );
            panel.ShowCapture(controlCenter.IsCapturing);
        };

        // CCM-003: the notices are one shared state, so the status bar shows what the panel shows.
        panel.NoticePublished += (_, published) => controlCenter.OnPanelNotice(published.Notice);
        controlCenter.OnPanelNotice(panel.CurrentNotice);

        // PRB-004: «Probar ahora» says [switching] in the panel, fixed, while the Control Center is hidden.
        var switching = new object();
        controlCenter.PanelNotice = message =>
        {
            if (message is null)
            {
                panel.ClearSticky(switching);
            }
            else
            {
                panel.ShowSticky(
                    switching,
                    new PanelNotice(message, new IconRef(SwitchingIcon), NoticeTone.Notice)
                );
            }
        };
    }
}
