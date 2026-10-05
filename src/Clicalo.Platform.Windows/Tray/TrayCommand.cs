namespace Clicalo.Platform.Windows.Tray;

/// <summary>The entries of the M2 tray menu (BUR-003); the value is the menu item id.</summary>
public enum TrayCommand
{
    /// <summary>«Mostrar panel» or «Ocultar panel», as the panel is hidden or visible.</summary>
    ShowHide = 1,

    /// <summary>«Soltar todo»: enabled while something is held (SEG-003).</summary>
    ReleaseAll = 2,

    /// <summary>«Salir»: releases everything and ends Clícalo.</summary>
    Exit = 3,
}
