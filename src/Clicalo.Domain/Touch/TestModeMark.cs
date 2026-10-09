namespace Clicalo.Domain.Touch;

/// <summary>
/// What a touch on a tile shows in test mode (TAC-008, CUA-009): ✓ on green when the filter of TAC-002 accepted it, ⊘
/// on red when it ignored it, for <c>Timings.TestMode.TestMarkDuration</c> (700 ms), with the notice [tmOk], [tShort] or
/// [tDouble]. The verdict is the gesture recognizer's: the panel never runs a second filter (blueprint §7.8).
/// </summary>
/// <param name="Accepted">Whether the touch counted.</param>
/// <param name="Reason">
/// Why it was ignored: <see cref="IgnoreReason.TooShort"/> or <see cref="IgnoreReason.Debounced"/>;
/// <see cref="IgnoreReason.None"/> when it counted.
/// </param>
public readonly record struct TestModeMark(bool Accepted, IgnoreReason Reason)
{
    /// <summary>An accepted touch: ✓ and [tmOk].</summary>
    public static TestModeMark Counted { get; } = new(true, IgnoreReason.None);

    /// <summary>
    /// The mark of a touch the filter ignored: ⊘ with [tShort] for a contact too short and with [tDouble] for one inside
    /// the debounce, the two verdicts the prototype marks. Any other ignored contact (a palm, a touch that moved or was
    /// cancelled, one outside every target or in the lock after a swipe) never pressed the tile, as in the prototype, and
    /// is not marked (<see langword="null"/>).
    /// </summary>
    /// <param name="reason">Why the recognizer ignored it.</param>
    public static TestModeMark? ForIgnored(IgnoreReason reason) =>
        reason is IgnoreReason.TooShort or IgnoreReason.Debounced
            ? new TestModeMark(false, reason)
            : null;
}
