namespace Clicalo.Presentation.ControlCenter;

/// <summary>The six sections of the side menu of the Control Center, in order (CCM-002).</summary>
public enum ControlCenterSection
{
    /// <summary>1. Atajos (<c>tune</c>), with the count of repeated combinations.</summary>
    Shortcuts,

    /// <summary>2. Plantillas (<c>auto_awesome</c>).</summary>
    Templates,

    /// <summary>3. General y panel (<c>display_settings</c>).</summary>
    Panel,

    /// <summary>4. Precisión táctil (<c>touch_app</c>).</summary>
    Touch,

    /// <summary>5. Sistema (<c>verified_user</c>), after a separator, with a count when there is an update.</summary>
    System,

    /// <summary>6. Acerca de y contacto (<c>favorite</c>).</summary>
    About,
}
