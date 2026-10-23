using Clicalo.Domain.Settings;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The first-time guide of the Full and Compact views (R-38, as PES-015 is for the Tab view): three steps beside the
/// panel, «Qué ver», the grid with its pages and the tools of the header. Pure.
/// </summary>
public static class PanelCoachRules
{
    /// <summary>Steps of the guide.</summary>
    public const int Steps = 3;

    /// <summary>
    /// Whether the guide shows beside the panel: with the Full or the Compact view on screen and the guide not finished,
    /// while neither Quick settings, which open in the same place (AJR-001), nor the welcome is open. The Full view the
    /// search of the Tab view borrows (BUS-006) is not the person's view, and has the guide of the bar instead.
    /// </summary>
    /// <param name="form">The form of the panel.</param>
    /// <param name="density">The saved view.</param>
    /// <param name="done">The guide was finished or skipped (<c>panelCoachDone</c>, ADR-0033).</param>
    /// <param name="quickSettingsOpen">Quick settings are open beside the panel.</param>
    /// <param name="welcomeOpen">The welcome is open.</param>
    public static bool Shows(
        PanelForm form,
        PanelDensity density,
        bool done,
        bool quickSettingsOpen,
        bool welcomeOpen
    ) =>
        PanelForms.IsPanel(form)
        && density != PanelDensity.Dock
        && !done
        && !quickSettingsOpen
        && !welcomeOpen;

    /// <summary>The step after <paramref name="step"/>, or <see langword="null"/> when the guide ends.</summary>
    /// <param name="step">The step on show, from 0.</param>
    public static int? NextStep(int step) => step + 1 < Steps ? step + 1 : null;
}
