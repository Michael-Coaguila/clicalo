namespace Clicalo.Domain.Dimming;

/// <summary>
/// What keeps the surfaces from dimming (docs/04 «Opacidad y atenuado»): while any of these is open or on, nothing
/// dims.
/// </summary>
[Flags]
public enum DimExceptions
{
    /// <summary>Nothing open: the surfaces may dim.</summary>
    None = 0,

    /// <summary>Something is held or latched: the panic strip and «Release all» are showing (SEG-002).</summary>
    Panic = 1,

    /// <summary>Quick settings are open.</summary>
    QuickSettings = 2,

    /// <summary>A context menu is open.</summary>
    ContextMenu = 4,

    /// <summary>The profile grid is open.</summary>
    ProfileGrid = 8,

    /// <summary>The search is open.</summary>
    Search = 16,

    /// <summary>The panel is in edit mode.</summary>
    EditMode = 32,

    /// <summary>A window beside the Tab bar is open (Pinned, Sticky keys, Profile picker).</summary>
    DockSideWindows = 64,

    /// <summary>The Control Center is open.</summary>
    ControlCenterOpen = 128,

    /// <summary>The welcome is open.</summary>
    WelcomeOpen = 256,
}
