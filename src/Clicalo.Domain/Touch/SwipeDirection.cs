namespace Clicalo.Domain.Touch;

/// <summary>Direction of a <see cref="GestureKind.Swipe"/>, as the finger moved.</summary>
public enum SwipeDirection
{
    /// <summary>Not a swipe.</summary>
    None,

    /// <summary>The finger moved to the left: next page.</summary>
    Left,

    /// <summary>The finger moved to the right: previous page.</summary>
    Right,
}
