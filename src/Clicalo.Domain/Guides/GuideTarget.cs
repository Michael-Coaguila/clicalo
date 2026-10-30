namespace Clicalo.Domain.Guides;

/// <summary>
/// The control a step of a guide points at (R-39): the view marks it, and the guide draws a ring around it, never over
/// it.
/// </summary>
public enum GuideTarget
{
    /// <summary>The grid of tiles of the panel.</summary>
    PanelGrid,

    /// <summary>★ Frecuentes in the «Qué ver» row.</summary>
    PanelFrequents,

    /// <summary>The profile button in the «Qué ver» row.</summary>
    PanelProfile,

    /// <summary>Auto/Fijo in the «Qué ver» row.</summary>
    PanelAutoFixed,

    /// <summary>The next page arrow of the pager.</summary>
    PanelPager,

    /// <summary>🔍 in the header.</summary>
    PanelSearch,

    /// <summary>✏ in the header.</summary>
    PanelEdit,

    /// <summary>Quick settings in the header.</summary>
    PanelQuick,

    /// <summary>The handle that moves the panel.</summary>
    PanelHandle,

    /// <summary>«−» in the header.</summary>
    PanelMinimize,

    /// <summary>The bubble itself.</summary>
    Bubble,

    /// <summary>The button that folds the bar.</summary>
    TabFold,

    /// <summary>The button that switches to the Full view.</summary>
    TabExpand,

    /// <summary>The lock that keeps the bar open.</summary>
    TabLock,

    /// <summary>Quick settings on the bar.</summary>
    TabQuick,

    /// <summary>★ Frecuentes on the bar.</summary>
    TabFrequents,

    /// <summary>The profile button on the bar.</summary>
    TabProfile,

    /// <summary>The tiles of the bar.</summary>
    TabTiles,

    /// <summary>The tools at the end of the bar.</summary>
    TabTools,

    /// <summary>The list of profiles of Atajos.</summary>
    ControlCenterProfiles,

    /// <summary>The shortcuts of the chosen profile.</summary>
    ControlCenterShortcuts,

    /// <summary>The editor of a shortcut.</summary>
    ControlCenterEditor,

    /// <summary>The templates to install.</summary>
    ControlCenterTemplates,

    /// <summary>«Crear con IA».</summary>
    ControlCenterAi,

    /// <summary>The view of General.</summary>
    ControlCenterView,

    /// <summary>The size of General.</summary>
    ControlCenterSize,

    /// <summary>«Primeros pasos» of General.</summary>
    ControlCenterStart,

    /// <summary>The presets of the touch filter.</summary>
    ControlCenterPresets,

    /// <summary>The test area of Precisión táctil.</summary>
    ControlCenterTestArea,

    /// <summary>The tabs of Sistema.</summary>
    ControlCenterSystemTabs,

    /// <summary>Feedback and contact.</summary>
    ControlCenterFeedback,
}
