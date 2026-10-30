namespace Clicalo.Domain.Guides;

/// <summary>The first-time guides, one per screen (R-39): the panel and its bubble, the Tab view and each section of the Control Center.</summary>
public enum GuideId
{
    /// <summary>The Full and Compact views, from the tiles to «−».</summary>
    Panel,

    /// <summary>The bubble the panel minimizes to.</summary>
    Bubble,

    /// <summary>The open bar of the Tab view (PES-015).</summary>
    Tab,

    /// <summary>Control Center › Atajos.</summary>
    Shortcuts,

    /// <summary>Control Center › Plantillas.</summary>
    Templates,

    /// <summary>Control Center › General y panel.</summary>
    General,

    /// <summary>Control Center › Precisión táctil.</summary>
    Touch,

    /// <summary>Control Center › Sistema.</summary>
    System,

    /// <summary>Control Center › Acerca de y contacto.</summary>
    About,
}
