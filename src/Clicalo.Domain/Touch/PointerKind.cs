namespace Clicalo.Domain.Touch;

/// <summary>The device behind a pointer contact (<c>POINTER_INPUT_TYPE</c>, blueprint §8.3).</summary>
public enum PointerKind
{
    /// <summary>A finger on a touch screen (<c>PT_TOUCH</c>): the primary input of Clícalo (ACC-007).</summary>
    Finger,

    /// <summary>A pen or stylus (<c>PT_PEN</c>).</summary>
    Pen,

    /// <summary>A mouse or touchpad (<c>PT_MOUSE</c>, <c>PT_TOUCHPAD</c>).</summary>
    Mouse,
}
