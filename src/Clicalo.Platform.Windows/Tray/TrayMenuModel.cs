using System.Collections.Immutable;
using Clicalo.Domain.Messages;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The tray menu and the icon's text for a <see cref="TrayState"/> (BUR-003, BUR-004): «Mostrar u ocultar», «Centro de
/// control», «Soltar todo» (enabled while something is held), «Pausar» or «Reanudar», and «Salir», in that order. Pure:
/// the controller formats the texts and runs the commands.
/// </summary>
public static class TrayMenuModel
{
    /// <summary>The entries of the menu, in order.</summary>
    /// <param name="state">What the tray shows.</param>
    public static ImmutableArray<TrayMenuEntry> Entries(TrayState state) =>
        [
            // Paused, the panel is hidden and showing it resumes (BUR-005: every way of hiding can be undone).
            new(
                TrayCommand.ShowHide,
                state.PanelVisible && !state.Paused ? L.HidePanel : L.Restore
            ),
            new(TrayCommand.ControlCenter, L.Cc),
            new(TrayCommand.ReleaseAll, L.ReleaseAll, IsEnabled: state.AnythingHeld),
            new(TrayCommand.Pause, state.Paused ? L.ResumeApp : L.PauseApp),
            new(TrayCommand.Exit, L.ExitApp),
        ];

    /// <summary>
    /// The icon's accessible text: the state is said there too (BUR-003: hidden; BUR-004: paused, which wins).
    /// </summary>
    /// <param name="state">What the tray shows.</param>
    public static Message Tooltip(TrayState state) =>
        state.Paused ? L.TrayPaused
        : state.PanelVisible ? L.AppName
        : L.TrayHidden;

    /// <summary>
    /// Whether the icon shows at 55 % (BUR-003: the hidden panel is told visually too; BUR-004: so is the pause).
    /// It changes exactly when <see cref="Tooltip"/> does.
    /// </summary>
    /// <param name="state">What the tray shows.</param>
    public static bool IsDimmed(TrayState state) => state.Paused || !state.PanelVisible;
}
