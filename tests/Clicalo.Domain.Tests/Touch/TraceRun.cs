using System.Collections.Immutable;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>What a recognizer reported for a <see cref="TouchTrace"/>.</summary>
/// <param name="Events">Every gesture, in order, including those of the final reset.</param>
/// <param name="Marks">For each step, how many gestures had been reported once it was fed.</param>
/// <param name="BeforeReset">How many gestures were reported before the final reset.</param>
/// <param name="ContactsAfterReset">Contacts still tracked after the final reset.</param>
internal sealed record TraceRun(
    IReadOnlyList<GestureEvent> Events,
    ImmutableArray<int> Marks,
    int BeforeReset,
    int ContactsAfterReset
)
{
    /// <summary>The gestures of one contact, in order.</summary>
    public IEnumerable<GestureEvent> Of(uint pointerId) =>
        Events.Where(e => e.PointerId == pointerId);
}
