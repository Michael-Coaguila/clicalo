namespace Clicalo.UI.Wpf.Windowing;

/// <summary>The three buttons of a window's title bar (D13).</summary>
public enum CaptionKind
{
    /// <summary>Minimize: the window goes to the taskbar.</summary>
    Minimize,

    /// <summary>Maximize, or restore while the window is maximized; it opens the Snap layouts of Windows 11.</summary>
    Maximize,

    /// <summary>Close; red under the pointer, as in every window of Windows.</summary>
    Close,
}
