namespace Clicalo.Domain.Library;

/// <summary>The eight mouse actions (EJE-009).</summary>
public enum MouseOp
{
    /// <summary>Right click.</summary>
    RightClick,

    /// <summary>Double click.</summary>
    DoubleClick,

    /// <summary>Middle click.</summary>
    MiddleClick,

    /// <summary>Drag: a toggle of the left button (EJE-007).</summary>
    Drag,

    /// <summary>Scroll up, repeated while held.</summary>
    ScrollUp,

    /// <summary>Scroll down, repeated while held.</summary>
    ScrollDown,

    /// <summary>Scroll left, repeated while held.</summary>
    ScrollLeft,

    /// <summary>Scroll right, repeated while held.</summary>
    ScrollRight,
}
