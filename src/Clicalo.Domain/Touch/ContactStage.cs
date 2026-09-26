namespace Clicalo.Domain.Touch;

/// <summary>Where one contact of <see cref="GestureRecognizer"/> is in its life.</summary>
internal enum ContactStage
{
    /// <summary>
    /// On a <see cref="TouchTargetKind.Tap"/> or <see cref="TouchTargetKind.TapOrLongPress"/> target: waiting for
    /// the lift (tap or ignored) and, on the second kind, for the long press deadline.
    /// </summary>
    Pending,

    /// <summary>The long press was reported: nothing else comes from this contact.</summary>
    LongPressed,

    /// <summary>On a <see cref="TouchTargetKind.Hold"/> target, waiting for the minimum contact before starting.</summary>
    HoldPending,

    /// <summary><see cref="GestureKind.HoldStart"/> was reported; a <see cref="GestureKind.HoldEnd"/> must follow.</summary>
    Holding,

    /// <summary>The hold ended because the contact left the target; only a swipe may still follow.</summary>
    HoldEnded,

    /// <summary>The contact will not act on a target; the reason is known, and a swipe may still follow (not a palm).</summary>
    Ignored,
}
