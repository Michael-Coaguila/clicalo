namespace Clicalo.Domain.Keys;

/// <summary>
/// Side of a modifier key. There is a single side mechanism (EDI-009): the sided keys of the catalog
/// (<c>lctrl</c>, <c>altgr</c>…) are shorthands for a modifier plus a side.
/// </summary>
public enum KeySide
{
    /// <summary>Either side. A generic modifier is sent as its left key (docs/03 §3).</summary>
    Any,

    /// <summary>The left key.</summary>
    Left,

    /// <summary>The right key.</summary>
    Right,
}
