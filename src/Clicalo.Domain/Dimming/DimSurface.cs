namespace Clicalo.Domain.Dimming;

/// <summary>The surfaces that follow the opacity and the automatic dimming (docs/04 «Opacidad y atenuado»).</summary>
public enum DimSurface
{
    /// <summary>The panel, in its Full and Compact forms.</summary>
    Panel,

    /// <summary>The edge bar of the Tab view.</summary>
    Dock,

    /// <summary>The handle of the edge bar: never below <c>Timings.Dimming.BubbleMinOpacity</c> (PES-004).</summary>
    DockHandle,

    /// <summary>The 64 px bubble: never below <c>Timings.Dimming.BubbleMinOpacity</c> (BUR-002).</summary>
    Bubble,
}
