namespace Clicalo.Platform.Windows.Tray;

/// <summary>The entries of the tray menu (BUR-003, BUR-004); the value is the menu item id.</summary>
public enum TrayCommand
{
    /// <summary>«Mostrar panel» or «Ocultar panel», as the panel is hidden or visible.</summary>
    ShowHide = 1,

    /// <summary>«Soltar todo»: enabled while something is held (SEG-003).</summary>
    ReleaseAll = 2,

    /// <summary>«Salir»: releases everything and ends Clícalo.</summary>
    Exit = 3,

    /// <summary>«Centro de control»: opens the Control Center (blueprint §8.1, CCM-004).</summary>
    ControlCenter = 4,

    /// <summary>
    /// «Pausar» or «Reanudar» (BUR-004): paused, the panel hides, the profile stops following the app and nothing is
    /// sent; pausing releases everything.
    /// </summary>
    Pause = 5,
}
