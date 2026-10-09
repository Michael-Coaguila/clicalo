namespace Clicalo.Domain.Touch;

/// <summary>How the contact followed by a <see cref="DragTracker"/> ended.</summary>
public enum DragEnd
{
    /// <summary>It was not the followed contact.</summary>
    None,

    /// <summary>It lifted before the drag threshold: a tap of the zone.</summary>
    Tapped,

    /// <summary>It dragged: the surface moved, and it is not a tap (PAN-004).</summary>
    Dragged,
}
