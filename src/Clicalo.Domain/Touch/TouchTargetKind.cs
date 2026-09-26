namespace Clicalo.Domain.Touch;

/// <summary>Which gestures a <see cref="TouchTarget"/> accepts.</summary>
public enum TouchTargetKind
{
    /// <summary>Tap only: arrows, page dots, close buttons.</summary>
    Tap,

    /// <summary>
    /// Tap, and a long press (<c>Timings.Touch.LongPress</c>) that opens the secondary action instead of executing
    /// (tiles of the grid, the fixed row and the bar; CUA-014).
    /// </summary>
    TapOrLongPress,

    /// <summary>
    /// Hold shortcuts: <see cref="GestureKind.HoldStart"/> once the contact passes the filter and
    /// <see cref="GestureKind.HoldEnd"/> when it lifts, is cancelled or leaves the extra hit area (EJE-004). No long
    /// press menu.
    /// </summary>
    Hold,
}
