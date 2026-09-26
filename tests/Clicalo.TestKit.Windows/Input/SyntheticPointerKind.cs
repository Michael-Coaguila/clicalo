namespace Clicalo.TestKit.Windows.Input;

/// <summary>The device a <see cref="SyntheticPointer"/> impersonates.</summary>
public enum SyntheticPointerKind
{
    /// <summary>A finger (<c>PT_TOUCH</c> through <c>InjectSyntheticPointerInput</c>).</summary>
    Finger,

    /// <summary>A pen (<c>PT_PEN</c> through <c>InjectSyntheticPointerInput</c>).</summary>
    Pen,

    /// <summary>A mouse (<c>SendInput</c> with absolute <c>MOUSEINPUT</c>).</summary>
    Mouse,
}
