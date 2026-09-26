namespace Clicalo.Application.Ports;

/// <summary>The kinds of non-activatable surfaces of the panel (blueprint §8.1).</summary>
public enum SurfaceKind
{
    /// <summary>The panel, in its Full and Compact forms.</summary>
    Panel,

    /// <summary>The edge bar of the Tab view.</summary>
    Dock,

    /// <summary>The handle of the edge bar.</summary>
    DockHandle,

    /// <summary>The 64 px bubble.</summary>
    Bubble,

    /// <summary>A side window: Pinned, Sticky keys, Profile picker, Quick settings.</summary>
    SideWindow,

    /// <summary>The context menu and the drop-downs of a surface.</summary>
    Menu,

    /// <summary>A floating notice.</summary>
    Notice,
}
