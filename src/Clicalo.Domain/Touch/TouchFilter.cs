namespace Clicalo.Domain.Touch;

/// <summary>
/// The single touch filter of TAC-002 (blueprint §7.8). The panel, the bar, the side windows, the test zone
/// (TAC-006) and test mode (TAC-008) all call this function: there is no second implementation.
/// </summary>
/// <remarks>
/// A value of zero switches its check off, as the Touch precision sliders show it («Desactivado», TAC-005): a
/// zero <see cref="TouchSettings.CancelMovePx"/> never ignores a contact for moving, a zero
/// <see cref="TouchSettings.MinContact"/> never for being short and a zero <see cref="TouchSettings.Debounce"/> never
/// for repeating.
/// </remarks>
public static class TouchFilter
{
    /// <summary>
    /// Evaluates a finished contact on one target, in this order: palm → <see cref="TouchVerdict.IgnoredPalm"/>;
    /// displacement above a non-zero <see cref="TouchSettings.CancelMovePx"/> → <see cref="TouchVerdict.IgnoredSwipe"/>;
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
    )
    {
        if (contact.PalmLike)
        {
            return TouchVerdict.IgnoredPalm;
        }

        if (settings.CancelMovePx > 0 && contact.MaxDisplacementPx > settings.CancelMovePx)
        {
            return TouchVerdict.IgnoredSwipe;
        }

        if (settings.MinContact > TimeSpan.Zero && contact.Duration < settings.MinContact)
        {
            return TouchVerdict.IgnoredShort;
        }

        if (IsDebounced(state, settings, now))
        {
            return TouchVerdict.IgnoredDouble;
        }

        state.LastAccepted = now;
        return TouchVerdict.Accepted;
    }

    /// <summary>
    /// True when a hold may start on the target now: in a hold, the minimum contact delays the start (the caller
    /// passes the start time, at least <see cref="TouchSettings.MinContact"/> after the contact went down) and the
    /// debounce applies to the start (TAC-002, DIS-20). A start that is allowed counts as an accepted touch: the
    /// caller then stores it in the state of the target, as <see cref="GestureRecognizer"/> does.
    /// </summary>
    /// <param name="state">Filter memory of the target.</param>
    /// <param name="settings">Active filter values.</param>
    /// <param name="now">The candidate start time.</param>
    public static bool CanStartHold(
        in ButtonFilterState state,
        in TouchSettings settings,
        DateTimeOffset now
    ) => !IsDebounced(state, settings, now);

    private static bool IsDebounced(
        in ButtonFilterState state,
        in TouchSettings settings,
        DateTimeOffset now
    ) =>
        settings.Debounce > TimeSpan.Zero
        && state.LastAccepted is { } last
        && now - last < settings.Debounce;
}
