namespace Clicalo.Domain.Touch;

/// <summary>What a <see cref="GestureEvent"/> reports (blueprint §7.8).</summary>
public enum GestureKind
{
    /// <summary>An accepted tap on a target: the contact passed the filter of TAC-002 and lifted.</summary>
    Tap,

    /// <summary>
    /// The contact rested on a <see cref="TouchTargetKind.TapOrLongPress"/> target for <c>Timings.Touch.LongPress</c>
    /// (600 ms) without moving past the cancel distance: the secondary action opens and no tap follows.
    /// </summary>
    LongPress,

    /// <summary>A <see cref="TouchTargetKind.Hold"/> target starts holding (after the minimum contact and the debounce).</summary>
    HoldStart,

    /// <summary>A hold ends; <see cref="GestureEvent.HoldEnd"/> says why.</summary>
    HoldEnd,

    /// <summary>
    /// A horizontal swipe of at least <c>Timings.Touch.SwipeMinDistancePx</c> (60 logical px) with
    /// |dy| &lt; <c>Timings.Touch.SwipeMaxSlope</c> × |dx|; targets then ignore touches for
    /// <c>Timings.Touch.PostSwipeLock</c>.
    /// </summary>
    Swipe,

    /// <summary>A contact that did not count; <see cref="GestureEvent.Ignored"/> says why (TAC-003 feedback).</summary>
    Ignored,
}
