using System.Collections.Immutable;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Touch;

/// <summary>
/// Turns the pointer frames of ONE surface into gestures on its targets (blueprint §7.8, ADR-0006): hit resolution
/// with the extra area and nearest center, the filter of TAC-002 (<see cref="TouchFilter"/>), long press, hold,
/// swipe and ignored contacts with their reason. Pure: it never reads a clock (the frames carry their timestamps and
/// <see cref="OnTick"/> receives the time), never allocates on the hot path (the caller owns the output list) and is
/// used only from the thread of its surface.
/// </summary>
/// <remarks>
/// <para>
/// Thresholds come from <c>Clicalo.Domain.Timing.Timings.Touch</c> (NFR-020): <c>LongPress</c> 600 ms,
/// <c>SwipeMinDistancePx</c> 60, <c>SwipeMaxSlope</c> 0.6, <c>PostSwipeLock</c> 300 ms, <c>PalmContactMinPx</c> and
/// the drag threshold max(<c>DragMinDistancePx</c>, cancel distance). Logical pixels are scaled with
/// <see cref="DpiScale"/>.
/// </para>
/// <para>
/// Every contact is followed on its own by its pointer identifier (EJE-006) and keeps, for its whole life, the target
/// it went down on and the values it started with (PAN-009). What each target kind produces:
/// </para>
/// <list type="bullet">
/// <item><see cref="TouchTargetKind.Tap"/>: <see cref="GestureKind.Tap"/> when the contact lifts inside the extra hit
/// area and passes <see cref="TouchFilter.Evaluate"/>; <see cref="GestureKind.Ignored"/> with the reason
/// otherwise.</item>
/// <item><see cref="TouchTargetKind.TapOrLongPress"/>: the same, or <see cref="GestureKind.LongPress"/> once the
/// contact has rested <c>LongPress</c> without passing the drag threshold or leaving the extra hit area (CUA-014);
/// no tap follows a long press.</item>
/// <item><see cref="TouchTargetKind.Hold"/>: <see cref="GestureKind.HoldStart"/> after the minimum contact if the
/// debounce allows it (<see cref="TouchFilter.CanStartHold"/>), then exactly one <see cref="GestureKind.HoldEnd"/>
/// when the contact lifts, is cancelled, leaves the extra hit area or the recognizer is reset (EJE-004, REG-03). No
/// long press.</item>
/// </list>
/// <para>
/// A contact that lifts after moving more than <c>SwipeMinDistancePx</c> horizontally with
/// |dy| &lt; <c>SwipeMaxSlope</c> × |dx| is a <see cref="GestureKind.Swipe"/> instead of a tap or an ignored touch,
/// wherever it started (also on a hold that it left); palms, long presses and holds still active never swipe. For
/// <c>PostSwipeLock</c> after a swipe, contacts that go down are ignored with <see cref="IgnoreReason.SwipeLock"/>
/// (they can still swipe). A palm-sized contact (ACC-007) never acts; a hold that already started is not ended by
/// the contact area growing. After a cancel, a contact produces nothing more.
/// </para>
/// <para>Every sample of a frame is processed at the frame's <see cref="PointerFrame.Timestamp"/>.</para>
/// </remarks>
public sealed class GestureRecognizer
{
    private const int InitialContactCapacity = 10;

    private ContactThresholds _thresholds;
    private ButtonFilterState[] _states = [];
    private ContactSlot[] _contacts = new ContactSlot[InitialContactCapacity];
    private int _count;
    private DateTimeOffset _now = DateTimeOffset.MinValue;
    private DateTimeOffset _swipeLockUntil = DateTimeOffset.MinValue;

    /// <summary>Creates a recognizer for a surface with the given filter values and DPI scale.</summary>
    /// <param name="settings">Active filter values (logical pixels).</param>
    /// <param name="dpiScale">Physical pixels per logical pixel of the surface's monitor (1.0 at 96 DPI).</param>
    public GestureRecognizer(TouchSettings settings, double dpiScale)
    {
        Validate(settings, dpiScale);
        Settings = settings;
        DpiScale = dpiScale;
        _thresholds = ContactThresholds.From(settings, dpiScale);
    }

    /// <summary>Active filter values, in logical pixels.</summary>
    public TouchSettings Settings { get; private set; }

    /// <summary>Physical pixels per logical pixel of the surface's current monitor.</summary>
    public double DpiScale { get; private set; }

    /// <summary>The targets hit-tested by <see cref="Feed"/>, in physical screen pixels.</summary>
    public ImmutableArray<TouchTarget> Targets { get; private set; } = [];

    /// <summary>Contacts currently down on the surface (drives the frozen layout of PAN-009).</summary>
    public int ActiveContacts => _count;

    /// <summary>
    /// The next time <see cref="OnTick"/> must run for a pending long press, hold start or swipe lock; null when
    /// nothing is pending. The surface schedules one <see cref="TimeProvider"/> timer for it.
    /// </summary>
    public DateTimeOffset? NextDeadline
    {
        get
        {
            DateTimeOffset? next = _swipeLockUntil > _now ? _swipeLockUntil : null;
            for (var i = 0; i < _count; i++)
            {
                ref readonly var contact = ref _contacts[i];
                if (contact.HasDeadline && (next is null || contact.Deadline < next.Value))
                {
                    next = contact.Deadline;
                }
            }

            return next;
        }
    }

    /// <summary>
    /// Replaces the targets after a layout change. Contacts already down keep the target they started on (the
    /// layout is frozen under the finger, PAN-009), and the filter memory of every target that keeps its identifier
    /// survives (TAC-002).
    /// </summary>
    /// <exception cref="ArgumentException">Two targets share an identifier.</exception>
    public void SetTargets(ImmutableArray<TouchTarget> targets)
    {
        var next = targets.IsDefault ? [] : targets;
        var states = new ButtonFilterState[next.Length];
        for (var i = 0; i < next.Length; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (next[j].Id == next[i].Id)
                {
                    throw new ArgumentException(
                        "Every target of a surface needs its own identifier.",
                        nameof(targets)
                    );
                }
            }

            var previous = IndexOf(next[i].Id);
            if (previous >= 0)
            {
                states[i] = _states[previous];
            }
        }

        Targets = next;
        _states = states;
    }

    /// <summary>
    /// Applies new filter values or a new DPI scale (the surface moved to another monitor). Contacts already down
    /// finish with the old values.
    /// </summary>
    public void Configure(TouchSettings settings, double dpiScale)
    {
        Validate(settings, dpiScale);
        Settings = settings;
        DpiScale = dpiScale;
        _thresholds = ContactThresholds.From(settings, dpiScale);
    }

    /// <summary>Feeds one frame and appends the gestures it completes to <paramref name="output"/>.</summary>
    /// <param name="frame">Every contact of one pointer frame, in time order with the previous frames.</param>
    /// <param name="output">Receives the gestures, in order; never cleared by the recognizer.</param>
    /// <remarks>
    /// Deadlines that fall before the frame (a long press or a hold start whose timer has not run yet) are reported
    /// first, at their own time, so the result never depends on when <see cref="OnTick"/> ran.
    /// </remarks>
    public void Feed(in PointerFrame frame, ICollection<GestureEvent> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var now = Advance(frame.Timestamp, output);
        if (frame.Samples.IsDefault)
        {
            return;
        }

        foreach (var sample in frame.Samples)
        {
            switch (sample.Phase)
            {
                case PointerPhase.Down:
                    Down(sample, now, output);
                    break;
                case PointerPhase.Move:
                    var moving = Find(sample.PointerId);
                    if (moving >= 0)
                    {
                        Move(ref _contacts[moving], sample, now, output);
                    }

                    break;
                case PointerPhase.Up:
                    var lifted = Find(sample.PointerId);
                    if (lifted >= 0)
                    {
                        Move(ref _contacts[lifted], sample, now, output);
                        Lift(ref _contacts[lifted], now, output);
                        Remove(lifted);
                    }

                    break;
                case PointerPhase.Cancel:
                    var canceled = Find(sample.PointerId);
                    if (canceled >= 0)
                    {
                        Cancel(ref _contacts[canceled], now, output);
                        Remove(canceled);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Advances time without input and appends the gestures that became due (a long press, a hold start after the
    /// minimum contact) to <paramref name="output"/>.
    /// </summary>
    /// <param name="now">The current time, on the same timeline as the frames.</param>
    /// <param name="output">Receives the gestures, in order.</param>
    public void OnTick(DateTimeOffset now, ICollection<GestureEvent> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _ = Advance(now, output);
    }

    /// <summary>
    /// Forgets every contact (surface hidden, session locked): each active hold ends with
    /// <see cref="HoldEndReason.Reset"/> in <paramref name="output"/> so no key stays down (REG-03). Deadlines that
    /// were due are dropped, not reported, and the swipe lock ends; the filter memory of the targets is kept.
    /// </summary>
    public void Reset(DateTimeOffset now, ICollection<GestureEvent> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (now > _now)
        {
            _now = now;
        }

        for (var i = 0; i < _count; i++)
        {
            ref readonly var contact = ref _contacts[i];
            if (contact.Stage == ContactStage.Holding)
            {
                output.Add(HoldEnd(contact, HoldEndReason.Reset, _now));
            }
        }

        Array.Clear(_contacts, 0, _count);
        _count = 0;
        _swipeLockUntil = DateTimeOffset.MinValue;
    }

    private static void Validate(in TouchSettings settings, double dpiScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiScale);
        if (!double.IsFinite(dpiScale))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpiScale),
                dpiScale,
                "The DPI scale must be finite."
            );
        }

        if (
            settings.Debounce < TimeSpan.Zero
            || settings.MinContact < TimeSpan.Zero
            || settings.HitSlopPx < 0
            || settings.CancelMovePx < 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings,
                "Touch filter values cannot be negative."
            );
        }
    }

    private static GestureEvent HoldEnd(
        in ContactSlot contact,
        HoldEndReason reason,
        DateTimeOffset now
    ) =>
        new(
            GestureKind.HoldEnd,
            contact.PointerId,
            contact.Target.Id,
            contact.Position,
            now,
            HoldEnd: reason
        );

    private static GestureEvent Ignored(
        in ContactSlot contact,
        IgnoreReason reason,
        DateTimeOffset now
    ) =>
        new(
            GestureKind.Ignored,
            contact.PointerId,
            contact.ReportedTarget,
            contact.Position,
            now,
            Ignored: reason
        );

    private static IgnoreReason ToReason(TouchVerdict verdict) =>
        verdict switch
        {
            TouchVerdict.IgnoredPalm => IgnoreReason.Palm,
            TouchVerdict.IgnoredSwipe => IgnoreReason.Moved,
            TouchVerdict.IgnoredShort => IgnoreReason.TooShort,
            TouchVerdict.IgnoredDouble => IgnoreReason.Debounced,
            _ => IgnoreReason.None,
        };

    private static void Ignore(ref ContactSlot contact, IgnoreReason reason)
    {
        contact.Stage = ContactStage.Ignored;
        contact.Reason = reason;
        contact.LongPressPending = false;
    }

    /// <summary>Moves the recognizer's time forward to <paramref name="time"/>, reporting every deadline on the way.</summary>
    private DateTimeOffset Advance(DateTimeOffset time, ICollection<GestureEvent> output)
    {
        var target = time > _now ? time : _now;
        while (true)
        {
            var due = -1;
            for (var i = 0; i < _count; i++)
            {
                ref readonly var contact = ref _contacts[i];
                if (
                    contact.HasDeadline
                    && contact.Deadline <= target
                    && (due < 0 || contact.Deadline < _contacts[due].Deadline)
                )
                {
                    due = i;
                }
            }

            if (due < 0)
            {
                break;
            }

            ref var slot = ref _contacts[due];
            if (slot.Deadline > _now)
            {
                _now = slot.Deadline;
            }

            if (slot.Stage == ContactStage.HoldPending)
            {
                TryStartHold(ref slot, _now, output);
            }
            else
            {
                slot.Stage = ContactStage.LongPressed;
                slot.LongPressPending = false;
                output.Add(
                    new GestureEvent(
                        GestureKind.LongPress,
                        slot.PointerId,
                        slot.Target.Id,
                        slot.Position,
                        _now
                    )
                );
            }
        }

        _now = target;
        return target;
    }

    private void Down(in PointerSample sample, DateTimeOffset now, ICollection<GestureEvent> output)
    {
        var existing = Find(sample.PointerId);
        if (existing >= 0)
        {
            // Windows reuses an identifier only after the contact ended: a second down means the end was lost.
            Cancel(ref _contacts[existing], now, output);
            Remove(existing);
        }

        if (_count == _contacts.Length)
        {
            Array.Resize(ref _contacts, _contacts.Length * 2);
        }

        ref var contact = ref _contacts[_count++];
        contact = default;
        contact.PointerId = sample.PointerId;
        contact.Thresholds = _thresholds;
        contact.DownPosition = sample.Position;
        contact.Position = sample.Position;
        contact.DownTime = now;
        contact.PalmLike = _thresholds.IsPalm(sample.Contact);
        var hit = HitResolver.Resolve(Targets.AsSpan(), sample.Position, _thresholds.HitSlop);
        if (hit >= 0)
        {
            contact.HasTarget = true;
            contact.Target = Targets[hit];
        }

        if (contact.PalmLike)
        {
            Ignore(ref contact, IgnoreReason.Palm);
        }
        else if (now < _swipeLockUntil)
        {
            Ignore(ref contact, IgnoreReason.SwipeLock);
        }
        else if (!contact.HasTarget)
        {
            Ignore(ref contact, IgnoreReason.NoTarget);
        }
        else if (contact.Target.Kind == TouchTargetKind.Hold)
        {
            var minContact = _thresholds.Settings.MinContact;
            if (minContact > TimeSpan.Zero)
            {
                contact.Stage = ContactStage.HoldPending;
                contact.Deadline = now + minContact;
            }
            else
            {
                TryStartHold(ref contact, now, output);
            }
        }
        else
        {
            contact.Stage = ContactStage.Pending;
            if (contact.Target.Kind == TouchTargetKind.TapOrLongPress)
            {
                contact.LongPressPending = true;
                contact.Deadline = now + Timings.Touch.LongPress;
            }
        }
    }

    private static void Move(
        ref ContactSlot contact,
        in PointerSample sample,
        DateTimeOffset now,
        ICollection<GestureEvent> output
    )
    {
        contact.Position = sample.Position;
        var displacement = contact.DownPosition.DistanceTo(sample.Position);
        if (displacement > contact.MaxDisplacement)
        {
            contact.MaxDisplacement = displacement;
        }

        var thresholds = contact.Thresholds;
        if (!contact.PalmLike && thresholds.IsPalm(sample.Contact))
        {
            contact.PalmLike = true;
        }

        if (
            contact.HasTarget
            && !contact.LeftTarget
            && !contact.Target.Bounds.Inflate(thresholds.HitSlop).Contains(sample.Position)
        )
        {
            contact.LeftTarget = true;
        }

        switch (contact.Stage)
        {
            case ContactStage.Pending:
                if (contact.PalmLike)
                {
                    Ignore(ref contact, IgnoreReason.Palm);
                }
                else if (contact.LeftTarget)
                {
                    Ignore(ref contact, IgnoreReason.Moved);
                }
                else if (contact.MaxDisplacement > thresholds.MoveSlop)
                {
                    contact.LongPressPending = false;
                }

                break;
            case ContactStage.HoldPending:
                if (contact.PalmLike)
                {
                    Ignore(ref contact, IgnoreReason.Palm);
                }
                else if (
                    contact.LeftTarget || thresholds.ExceedsCancelMove(contact.MaxDisplacement)
                )
                {
                    Ignore(ref contact, IgnoreReason.Moved);
                }

                break;
            case ContactStage.Holding:
                if (contact.LeftTarget)
                {
                    contact.Stage = ContactStage.HoldEnded;
                    output.Add(HoldEnd(contact, HoldEndReason.LeftTarget, now));
                }

                break;
            case ContactStage.Ignored:
                if (contact.PalmLike)
                {
                    contact.Reason = IgnoreReason.Palm;
                }

                break;
        }
    }

    private void Lift(ref ContactSlot contact, DateTimeOffset now, ICollection<GestureEvent> output)
    {
        switch (contact.Stage)
        {
            case ContactStage.Holding:
                output.Add(HoldEnd(contact, HoldEndReason.Lifted, now));
                return;
            case ContactStage.LongPressed:
                return;
        }

        if (!contact.PalmLike && contact.Thresholds.IsSwipe(contact.DownPosition, contact.Position))
        {
            var direction =
                contact.Position.X < contact.DownPosition.X
                    ? SwipeDirection.Left
                    : SwipeDirection.Right;
            output.Add(
                new GestureEvent(
                    GestureKind.Swipe,
                    contact.PointerId,
                    null,
                    contact.Position,
                    now,
                    Swipe: direction
                )
            );
            _swipeLockUntil = now + Timings.Touch.PostSwipeLock;
            return;
        }

        switch (contact.Stage)
        {
            case ContactStage.HoldPending:
                output.Add(Ignored(contact, IgnoreReason.TooShort, now));
                break;
            case ContactStage.Ignored:
                output.Add(Ignored(contact, contact.Reason, now));
                break;
            case ContactStage.Pending:
                Evaluate(ref contact, now, output);
                break;
        }
    }

    private void Evaluate(
        ref ContactSlot contact,
        DateTimeOffset now,
        ICollection<GestureEvent> output
    )
    {
        var thresholds = contact.Thresholds;
        var summary = new ContactSummary(
            now - contact.DownTime,
            contact.MaxDisplacement / thresholds.DpiScale,
            contact.PalmLike
        );
        var settings = thresholds.Settings;
        var index = IndexOf(contact.Target.Id);
        TouchVerdict verdict;
        if (index >= 0)
        {
            verdict = TouchFilter.Evaluate(ref _states[index], summary, settings, now);
        }
        else
        {
            // The target left the layout while the finger was on it: no filter memory to keep.
            var detached = default(ButtonFilterState);
            verdict = TouchFilter.Evaluate(ref detached, summary, settings, now);
        }

        output.Add(
            verdict == TouchVerdict.Accepted
                ? new GestureEvent(
                    GestureKind.Tap,
                    contact.PointerId,
                    contact.Target.Id,
                    contact.Position,
                    now
                )
                : Ignored(contact, ToReason(verdict), now)
        );
    }

    private void TryStartHold(
        ref ContactSlot contact,
        DateTimeOffset now,
        ICollection<GestureEvent> output
    )
    {
        var settings = contact.Thresholds.Settings;
        var index = IndexOf(contact.Target.Id);
        var state = index >= 0 ? _states[index] : default;
        if (!TouchFilter.CanStartHold(state, settings, now))
        {
            Ignore(ref contact, IgnoreReason.Debounced);
            return;
        }

        if (index >= 0)
        {
            _states[index].LastAccepted = now;
        }

        contact.Stage = ContactStage.Holding;
        output.Add(
            new GestureEvent(
                GestureKind.HoldStart,
                contact.PointerId,
                contact.Target.Id,
                contact.Position,
                now
            )
        );
    }

    private static void Cancel(
        ref ContactSlot contact,
        DateTimeOffset now,
        ICollection<GestureEvent> output
    )
    {
        switch (contact.Stage)
        {
            case ContactStage.Holding:
                output.Add(HoldEnd(contact, HoldEndReason.Canceled, now));
                break;
            case ContactStage.LongPressed:
            case ContactStage.HoldEnded:
                break;
            default:
                output.Add(Ignored(contact, IgnoreReason.Canceled, now));
                break;
        }
    }

    private int Find(uint pointerId)
    {
        for (var i = 0; i < _count; i++)
        {
            if (_contacts[i].PointerId == pointerId)
            {
                return i;
            }
        }

        return -1;
    }

    private int IndexOf(TouchTargetId id)
    {
        var targets = Targets.AsSpan();
        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Removes the contact at <paramref name="index"/>, keeping the others in the order they went down.</summary>
    private void Remove(int index)
    {
        _count--;
        if (index < _count)
        {
            Array.Copy(_contacts, index + 1, _contacts, index, _count - index);
        }

        _contacts[_count] = default;
    }
}
