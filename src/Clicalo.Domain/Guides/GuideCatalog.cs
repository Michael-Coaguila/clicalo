using System.Collections.Immutable;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Guides;

/// <summary>
/// The steps of every guide (R-39), in the order the person meets the controls. A step waits only for an action that is
/// safe to ask for (★, the profile button, a page, «−», the bubble, a profile, the test area, a tab of Sistema); a tile
/// never, because it acts on the person's app.
/// </summary>
public static class GuideCatalog
{
    private static readonly ImmutableArray<GuideStep> PanelSteps =
    [
        new(GuideTarget.PanelGrid, L.GGridT, L.GGridD),
        new(GuideTarget.PanelFrequents, L.Freq, L.GFreqD, GuideSignal.FrequentsShown),
        new(GuideTarget.PanelProfile, L.GProfileT, L.GProfileD, GuideSignal.ProfileButtonUsed),
        new(GuideTarget.PanelAutoFixed, L.GAutoT, L.GAutoD),
        new(
            GuideTarget.PanelPager,
            L.GPagesT,
            L.GPagesD,
            GuideSignal.PageTurned,
            OnlyWithTarget: true
        ),
        new(GuideTarget.PanelSearch, L.SearchA, L.GSearchD),
        new(GuideTarget.PanelEdit, L.Edit, L.GEditD),
        new(GuideTarget.PanelQuick, L.Quick, L.GQuickD),
        new(GuideTarget.PanelHandle, L.GMoveT, L.GMoveD),
        new(GuideTarget.PanelMinimize, L.Min, L.GMinD, GuideSignal.Minimized),
    ];

    private static readonly ImmutableArray<GuideStep> BubbleSteps =
    [
        new(GuideTarget.Bubble, L.GBubbleT, L.GBubbleD, GuideSignal.Restored),
    ];

    private static readonly ImmutableArray<GuideStep> TabSteps =
    [
        new(GuideTarget.TabFold, L.GFoldT, L.GFoldD),
        new(GuideTarget.TabExpand, L.GExpandT, L.GExpandD),
        new(GuideTarget.TabLock, L.GLockT, L.GLockD),
        new(GuideTarget.TabQuick, L.Quick, L.GQuickD),
        new(GuideTarget.TabFrequents, L.Freq, L.GFreqD, GuideSignal.FrequentsShown),
        new(GuideTarget.TabProfile, L.GProfileT, L.GProfileD, GuideSignal.ProfileButtonUsed),
        new(GuideTarget.TabTiles, L.GGridT, L.GGridD),
        new(GuideTarget.TabTools, L.Co3t, L.Co3d),
    ];

    private static readonly ImmutableArray<GuideStep> ShortcutsSteps =
    [
        new(GuideTarget.ControlCenterProfiles, L.Profiles, L.GProfilesD, GuideSignal.ProfileChosen),
        new(GuideTarget.ControlCenterShortcuts, L.GListT, L.GListD),
        new(GuideTarget.ControlCenterEditor, L.GEditorT, L.GEditorD),
    ];

    private static readonly ImmutableArray<GuideStep> TemplatesSteps =
    [
        new(GuideTarget.ControlCenterTemplates, L.NavTpl, L.GTplD),
        new(GuideTarget.ControlCenterAi, L.AiTitle, L.GAiD),
    ];

    private static readonly ImmutableArray<GuideStep> GeneralSteps =
    [
        new(GuideTarget.ControlCenterView, L.GViewT, L.GViewD),
        new(GuideTarget.ControlCenterSize, L.GSizeT, L.GSizeD),
        new(GuideTarget.ControlCenterStart, L.SecStart, L.GStartD),
    ];

    private static readonly ImmutableArray<GuideStep> TouchSteps =
    [
        new(GuideTarget.ControlCenterPresets, L.GPresetsT, L.GPresetsD),
        new(GuideTarget.ControlCenterTestArea, L.TestZone, L.GZoneD, GuideSignal.TestTapped),
    ];

    private static readonly ImmutableArray<GuideStep> SystemSteps =
    [
        new(GuideTarget.ControlCenterSystemTabs, L.GTabsT, L.GTabsD, GuideSignal.SystemTabChanged),
    ];

    private static readonly ImmutableArray<GuideStep> AboutSteps =
    [
        new(GuideTarget.ControlCenterFeedback, L.FbTitle, L.GFbD),
    ];

    /// <summary>The steps of <paramref name="guide"/>, in order.</summary>
    /// <param name="guide">The guide.</param>
    public static ImmutableArray<GuideStep> Steps(GuideId guide) =>
        guide switch
        {
            GuideId.Panel => PanelSteps,
            GuideId.Bubble => BubbleSteps,
            GuideId.Tab => TabSteps,
            GuideId.Shortcuts => ShortcutsSteps,
            GuideId.Templates => TemplatesSteps,
            GuideId.General => GeneralSteps,
            GuideId.Touch => TouchSteps,
            GuideId.System => SystemSteps,
            GuideId.About => AboutSteps,
            _ => throw new ArgumentOutOfRangeException(nameof(guide), guide, null),
        };

    /// <summary>
    /// The name of the window or card of <paramref name="guide"/> for UI Automation (REG-06), the same in every step:
    /// [panelCoachName], [bubbleGuideName] and [dockCoachName]; a section of the Control Center is
    /// [guideOf] with <see cref="Section"/>.
    /// </summary>
    /// <param name="guide">The guide.</param>
    public static Message? Name(GuideId guide) =>
        guide switch
        {
            GuideId.Panel => L.PanelCoachName,
            GuideId.Bubble => L.BubbleGuideName,
            GuideId.Tab => L.DockCoachName,
            _ => null,
        };

    /// <summary>The name of the section of the Control Center <paramref name="guide"/> explains, as the rail says it.</summary>
    /// <param name="guide">The guide.</param>
    public static Message? Section(GuideId guide) =>
        guide switch
        {
            GuideId.Shortcuts => L.NavShort,
            GuideId.Templates => L.NavTpl,
            GuideId.General => L.NavPanel,
            GuideId.Touch => L.NavTouch,
            GuideId.System => L.NavSys,
            GuideId.About => L.NavAbout2,
            _ => null,
        };
}
