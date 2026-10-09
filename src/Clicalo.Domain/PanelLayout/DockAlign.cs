namespace Clicalo.Domain.PanelLayout;

/// <summary>How a window beside the bar lines up with what it goes beside (<see cref="DockGeometry.Beside"/>).</summary>
public enum DockAlign
{
    /// <summary>With its top (vertical edge) or its left (horizontal edge): the profile grid and the guide.</summary>
    Start,

    /// <summary>With its bottom or its right: the «Pinned» and «Sticky keys» windows (PES-010).</summary>
    End,

    /// <summary>Centered on it: «Release all» beside the closed handle (PES-013).</summary>
    Center,
}
