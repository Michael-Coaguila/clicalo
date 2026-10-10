using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Application.Interaction;

/// <summary>
/// The notices of the product and which one shows now (AVI-002, blueprint §8.2): pure and immutable, part of the
/// <see cref="InteractionState"/> and changed only through <see cref="InteractionReducer"/>. One notice shows at a
/// time (<see cref="Shown"/>):
/// <list type="bullet">
/// <item>a notice that was posted shows at once, for its duration; a plain one is replaced by the next;</item>
/// <item>a safety notice or one with [undo] is never lost: when another notice arrives it waits and shows again, for
/// its whole duration, once the current one ends; among the waiting ones a safety notice goes first, then in order of
/// arrival. Only the newest notice with [undo] is kept, because that is the operation the button undoes;</item>
/// <item>a fixed notice shows whenever no posted one does, for as long as its owner keeps it: a posted notice covers
/// it for a while and it comes back afterwards. With several, the last one shown is on top.</item>
/// </list>
/// An operation that changes nothing returns the same instance.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The blueprint (§6.4, §8.2) names this type NoticeQueue: it is the queue of the notices, not a collection type."
)]
public sealed class NoticeQueue
{
    private NoticeQueue(
        Notice? current,
        TimeSpan currentDuration,
        DateTimeOffset? endsAt,
        ImmutableArray<WaitingNotice> waiting,
        ImmutableArray<StickyNotice> sticky
    )
    {
        Current = current;
        CurrentDuration = currentDuration;
        EndsAt = endsAt;
        Waiting = waiting;
        Sticky = sticky;
    }

    /// <summary>No notice: the bars are at rest.</summary>
    public static NoticeQueue Empty { get; } = new(null, TimeSpan.Zero, null, [], []);

    /// <summary>The posted notice on show, or null.</summary>
    public Notice? Current { get; }

    /// <summary>The whole time <see cref="Current"/> shows for.</summary>
    public TimeSpan CurrentDuration { get; }

    /// <summary>When <see cref="Current"/> ends; null without one. The owner of the state ticks then.</summary>
    public DateTimeOffset? EndsAt { get; }

    /// <summary>The safety notices and the one with [undo] that wait for their turn, in order of arrival.</summary>
    public ImmutableArray<WaitingNotice> Waiting { get; }

    /// <summary>The fixed notices, the last one shown at the end.</summary>
    public ImmutableArray<StickyNotice> Sticky { get; }

    /// <summary>The one notice on show: the posted one, else the fixed one on top, else null (at rest).</summary>
    public Notice? Shown => Current ?? (Sticky.IsEmpty ? null : Sticky[^1].Notice);

    /// <summary>The owner of the fixed notice on show; null while a posted notice shows or at rest.</summary>
    public object? ShownOwner => Current is null && !Sticky.IsEmpty ? Sticky[^1].Owner : null;

    /// <summary>Posts <paramref name="notice"/>: it shows now, for <paramref name="duration"/>.</summary>
    /// <param name="notice">The notice.</param>
    /// <param name="duration">How long it shows.</param>
    /// <param name="now">The current time.</param>
    public NoticeQueue Post(Notice notice, TimeSpan duration, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        var queue = Advance(now);
        var waiting = queue.Waiting.RemoveAll(other => Supersedes(notice, other.Notice));
        if (
            queue.Current is { Kind: not NoticeKind.Normal } displaced
            && !Supersedes(notice, displaced)
        )
        {
            waiting = waiting.Add(new WaitingNotice(displaced, queue.CurrentDuration));
        }

        return new NoticeQueue(notice, duration, now + duration, waiting, queue.Sticky);
    }

    /// <summary>
    /// Shows the fixed notice of <paramref name="owner"/>, on top of the other fixed ones. A plain notice on show gives
    /// way to it, and then a waiting safety notice or the one with [undo] shows first; a safety notice or one with
    /// [undo] on show ends first. Telling the same state again changes nothing.
    /// </summary>
    /// <param name="owner">Whose state it tells.</param>
    /// <param name="notice">The notice.</param>
    /// <param name="now">The current time.</param>
    public NoticeQueue ShowSticky(object owner, Notice notice, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(notice);
        foreach (var other in Sticky)
        {
            if (ReferenceEquals(other.Owner, owner) && other.Notice == notice)
            {
                return this;
            }
        }

        var sticky = Sticky
            .RemoveAll(other => ReferenceEquals(other.Owner, owner))
            .Add(new StickyNotice(owner, notice));
        var queue = new NoticeQueue(Current, CurrentDuration, EndsAt, Waiting, sticky);
        return Current is { Kind: NoticeKind.Normal } ? queue.Next(Waiting, now) : queue;
    }

    /// <summary>The state of <paramref name="owner"/> ended: its fixed notice goes away; every other notice stays.</summary>
    /// <param name="owner">The owner given to <see cref="ShowSticky"/>.</param>
    public NoticeQueue ClearSticky(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var sticky = Sticky.RemoveAll(other => ReferenceEquals(other.Owner, owner));
        return sticky.Length == Sticky.Length
            ? this
            : new NoticeQueue(Current, CurrentDuration, EndsAt, Waiting, sticky);
    }

    /// <summary>
    /// The clock moved: when the notice on show reached its end, the next waiting one shows (a safety notice first), for
    /// its whole duration from <paramref name="now"/>.
    /// </summary>
    /// <param name="now">The current time.</param>
    public NoticeQueue Advance(DateTimeOffset now) =>
        Current is null || EndsAt is not { } end || now < end ? this : Next(Waiting, now);

    /// <summary>
    /// The notices of <paramref name="kind"/> no longer apply (the operation was undone): they go away, on show or
    /// waiting, and the next one shows.
    /// </summary>
    /// <param name="kind">The kind to drop.</param>
    /// <param name="now">The current time.</param>
    public NoticeQueue Dismiss(NoticeKind kind, DateTimeOffset now)
    {
        var waiting = Waiting.RemoveAll(other => other.Notice.Kind == kind);
        if (Current?.Kind == kind)
        {
            return Next(waiting, now);
        }

        return waiting.Length == Waiting.Length
            ? this
            : new NoticeQueue(Current, CurrentDuration, EndsAt, waiting, Sticky);
    }

    /// <summary>
    /// Whether <paramref name="newer"/> makes <paramref name="older"/> pointless: the same notice again, or a newer
    /// operation to undo.
    /// </summary>
    private static bool Supersedes(Notice newer, Notice older) =>
        newer == older || (newer.Kind == NoticeKind.Undo && older.Kind == NoticeKind.Undo);

    private NoticeQueue Next(ImmutableArray<WaitingNotice> waiting, DateTimeOffset now)
    {
        if (waiting.IsEmpty)
        {
            return new NoticeQueue(null, TimeSpan.Zero, null, waiting, Sticky);
        }

        var next = waiting[0];
        foreach (var candidate in waiting)
        {
            if (candidate.Notice.Kind > next.Notice.Kind)
            {
                next = candidate;
            }
        }

        return new NoticeQueue(
            next.Notice,
            next.Duration,
            now + next.Duration,
            waiting.Remove(next),
            Sticky
        );
    }
}
