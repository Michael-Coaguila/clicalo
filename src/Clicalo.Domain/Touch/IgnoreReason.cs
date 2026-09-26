namespace Clicalo.Domain.Touch;

/// <summary>Why a contact produced <see cref="GestureKind.Ignored"/> instead of a gesture (TAC-002).</summary>
public enum IgnoreReason
{
    /// <summary>Not ignored.</summary>
    None,

    /// <summary>It moved further than the cancel distance and was not a swipe (TAC-002 step 1).</summary>
    Moved,

    /// <summary>It was shorter than the minimum contact (TAC-002 step 2).</summary>
    TooShort,

    /// <summary>The same target accepted a touch less than the debounce ago (TAC-002 step 3).</summary>
    Debounced,

    /// <summary>The contact area is palm-sized (ACC-007).</summary>
    Palm,

    /// <summary>It landed outside every target and its extra hit area.</summary>
    NoTarget,

    /// <summary>It started within the lock that follows a swipe.</summary>
    SwipeLock,

    /// <summary>The system cancelled the contact before it lifted.</summary>
    Canceled,
}
