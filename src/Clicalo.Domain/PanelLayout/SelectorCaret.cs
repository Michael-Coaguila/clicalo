namespace Clicalo.Domain.PanelLayout;

/// <summary>The end mark of the profile button of the selector (SEL-001).</summary>
public enum SelectorCaret
{
    /// <summary>▾: the profile grid is closed; a tap opens it.</summary>
    Expand,

    /// <summary>▴: the profile grid is open; a tap closes it.</summary>
    Collapse,

    /// <summary>↶: Frequents is in view; a tap returns to the profile it shows (PER-004).</summary>
    Return,
}
