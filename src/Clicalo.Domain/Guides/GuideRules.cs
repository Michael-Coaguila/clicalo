using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Guides;

/// <summary>
/// When a guide shows and which of its steps (R-39). One guide at a time: none while the welcome or the Control Center
/// is open on top of the panel, and each one until it is finished or skipped. Pure.
/// </summary>
public static class GuideRules
{
    /// <summary>
    /// The guide of the panel, the bar or the bubble that shows now, if any: the Tab view's while the bar is open with
    /// nothing beside it (PES-015); the panel's with the Full or Compact view on screen and Quick settings closed, as
    /// they open in the same place (AJR-001), but never on the Full view the search of the bar borrows (BUS-006); the
    /// bubble's while it is on screen.
    /// </summary>
    /// <param name="form">The form of the panel.</param>
    /// <param name="settings">The settings: the view and what is done.</param>
    /// <param name="besideBarOpen">A window is open beside the bar (a flyout, Quick settings, the menu of a shortcut).</param>
    /// <param name="quickSettingsOpen">Quick settings are open.</param>
    /// <param name="welcomeOpen">The welcome is open.</param>
    /// <param name="controlCenterOpen">The Control Center is open, with guides of its own.</param>
    public static GuideId? OnSurfaces(
        PanelForm form,
        UserSettings settings,
        bool besideBarOpen,
        bool quickSettingsOpen,
        bool welcomeOpen,
        bool controlCenterOpen
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (welcomeOpen || controlCenterOpen)
        {
            return null;
        }

        return form switch
        {
            PanelForm.DockOpen
                when DockRules.ShowsCoach(
                    barOpen: true,
                    GuideProgress.IsDone(settings, GuideId.Tab),
                    besideBarOpen
                ) => GuideId.Tab,
            PanelForm.Full
            or PanelForm.Compact
                when settings.Density != PanelDensity.Dock
                    && !quickSettingsOpen
                    && !GuideProgress.IsDone(settings, GuideId.Panel) => GuideId.Panel,
            PanelForm.Bubble when !GuideProgress.IsDone(settings, GuideId.Bubble) => GuideId.Bubble,
            _ => null,
        };
    }

    /// <summary>The guide of a section of the Control Center, until it is finished or skipped.</summary>
    /// <param name="section">The guide of the section in view.</param>
    /// <param name="settings">The settings: what is done.</param>
    public static GuideId? InControlCenter(GuideId section, UserSettings settings) =>
        GuideProgress.IsDone(settings, section) ? null : section;

    /// <summary>
    /// The step of <paramref name="guide"/> to show from <paramref name="step"/> on: itself, or the next one whose
    /// control is on screen when it only shows with it (the pager with a single page); <see langword="null"/> when
    /// none is left and the guide ends.
    /// </summary>
    /// <param name="guide">The guide.</param>
    /// <param name="step">The step reached, from 0.</param>
    /// <param name="onScreen">Whether a control is on screen now.</param>
    public static int? Showing(GuideId guide, int step, Func<GuideTarget, bool> onScreen)
    {
        ArgumentNullException.ThrowIfNull(onScreen);
        var steps = GuideCatalog.Steps(guide);
        for (var i = Math.Max(0, step); i < steps.Length; i++)
        {
            if (!steps[i].OnlyWithTarget || onScreen(steps[i].Target))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="signal"/> is what the step on show waits for, so the guide goes on by itself.</summary>
    /// <param name="guide">The guide.</param>
    /// <param name="step">The step on show.</param>
    /// <param name="signal">What the person did.</param>
    public static bool Completes(GuideId guide, int step, GuideSignal signal)
    {
        var steps = GuideCatalog.Steps(guide);
        return step >= 0 && step < steps.Length && steps[step].Awaits == signal;
    }
}
