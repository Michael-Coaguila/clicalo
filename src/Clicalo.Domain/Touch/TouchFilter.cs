using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Touch;

/// <summary>
/// The single touch filter of TAC-002 (blueprint §7.8). The panel, the bar, the side windows, the test zone
/// (TAC-006) and test mode (TAC-008) all call this function: there is no second implementation.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the pointer package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public static class TouchFilter
{
    /// <summary>
    /// Evaluates a finished contact on one target, in this order: palm → <see cref="TouchVerdict.IgnoredPalm"/>;
    /// displacement above <see cref="TouchSettings.CancelMovePx"/> → <see cref="TouchVerdict.IgnoredSwipe"/>;
    /// duration below a non-zero <see cref="TouchSettings.MinContact"/> → <see cref="TouchVerdict.IgnoredShort"/>;
    /// last accepted touch on THIS target less than a non-zero <see cref="TouchSettings.Debounce"/> ago →
    /// <see cref="TouchVerdict.IgnoredDouble"/>; otherwise <see cref="TouchVerdict.Accepted"/> and
    /// <paramref name="state"/> stores <paramref name="now"/>. Ignored touches leave <paramref name="state"/> unchanged.
    /// </summary>
    /// <param name="state">Filter memory of the target; updated only when the touch is accepted.</param>
    /// <param name="contact">The finished contact, with distances in the same unit as <paramref name="settings"/>.</param>
    /// <param name="settings">Active filter values.</param>
    /// <param name="now">When the contact ended.</param>
    public static TouchVerdict Evaluate(
        ref ButtonFilterState state,
        in ContactSummary contact,
        in TouchSettings settings,
        DateTimeOffset now
    ) => throw new NotImplementedException("M1 pointer package: TAC-002 filter.");

    /// <summary>
    /// True when a hold may start on the target now: in a hold, the minimum contact delays the start and the
    /// debounce applies to the start (TAC-002, DIS-20).
    /// </summary>
    /// <param name="state">Filter memory of the target.</param>
    /// <param name="settings">Active filter values.</param>
    /// <param name="now">The candidate start time.</param>
    public static bool CanStartHold(
        in ButtonFilterState state,
        in TouchSettings settings,
        DateTimeOffset now
    ) => throw new NotImplementedException("M1 pointer package: TAC-002 hold start.");
}
