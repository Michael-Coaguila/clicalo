using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using CsCheck;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// Properties of the recognizer over generated traces (<see cref="TouchTraceGen"/>): whatever the fingers do, an
/// ignored touch never acts, one target's debounce never changes another's outcome, nothing follows a cancel or a
/// lift, every hold is released exactly once and accepted touches on one target are never closer than the debounce.
/// </summary>
[Trait("Req", "TAC-002")]
public sealed class GestureRecognizerPropertyTests
{
    private const int Iterations = 1_500;

    [Fact]
    public void The_generated_traces_reach_every_gesture_and_every_reason()
    {
        var seen = new HashSet<(GestureKind, IgnoreReason, HoldEndReason)>();
        TouchTraceGen.Trace.Sample(
            trace =>
            {
                foreach (var gesture in trace.Run().Events)
                {
                    seen.Add((gesture.Kind, gesture.Ignored, gesture.HoldEnd));
                }
            },
            iter: Iterations,
            threads: 1
        );

        // Without this the properties below could pass on traces that never reach the interesting cases.
        (GestureKind, IgnoreReason, HoldEndReason)[] expected =
        [
            (GestureKind.Tap, IgnoreReason.None, HoldEndReason.None),
            (GestureKind.LongPress, IgnoreReason.None, HoldEndReason.None),
            (GestureKind.HoldStart, IgnoreReason.None, HoldEndReason.None),
            (GestureKind.HoldEnd, IgnoreReason.None, HoldEndReason.Lifted),
            (GestureKind.HoldEnd, IgnoreReason.None, HoldEndReason.Canceled),
            (GestureKind.HoldEnd, IgnoreReason.None, HoldEndReason.LeftTarget),
            (GestureKind.Swipe, IgnoreReason.None, HoldEndReason.None),
            (GestureKind.Ignored, IgnoreReason.Moved, HoldEndReason.None),
            (GestureKind.Ignored, IgnoreReason.TooShort, HoldEndReason.None),
            (GestureKind.Ignored, IgnoreReason.Debounced, HoldEndReason.None),
            (GestureKind.Ignored, IgnoreReason.Palm, HoldEndReason.None),
            (GestureKind.Ignored, IgnoreReason.NoTarget, HoldEndReason.None),
            (GestureKind.Ignored, IgnoreReason.SwipeLock, HoldEndReason.None),
            (GestureKind.Ignored, IgnoreReason.Canceled, HoldEndReason.None),
        ];
        expected.Except(seen).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TAC-003")]
    public void An_ignored_contact_never_acts_and_each_contact_has_at_most_one_outcome() =>
        TouchTraceGen.Trace.Sample(
            trace =>
            {
                var run = trace.Run();
                foreach (var contact in trace.Contacts)
                {
                    var events = run.Of(contact.PointerId).ToList();
                    var outcomes = events.Count(e =>
                        e.Kind is GestureKind.Tap or GestureKind.LongPress or GestureKind.Ignored
                    );
                    outcomes.ShouldBeLessThanOrEqualTo(1);
                    if (events.Any(e => e.Kind == GestureKind.Ignored))
                    {
                        events.ShouldAllBe(e => e.Kind == GestureKind.Ignored);
                    }
                }
            },
            iter: Iterations,
            print: t => t.ToString()
        );

    [Fact]
    public void The_debounce_of_one_target_never_changes_the_outcome_on_another() =>
        TouchTraceGen.QuietTrace.Sample(
            trace =>
            {
                var slop = HitSlop(trace);
                var owner = trace.Contacts.ToDictionary(
                    c => c.PointerId,
                    c => HitResolver.Resolve(trace.Targets.AsSpan(), c.Start, slop)
                );
                var target = owner.Values.First();
                var full = trace.Run();
                var alone = trace.Run(
                    TouchTraceGen.Steps(trace, c => owner[c.PointerId] == target, trace.Ticks)
                );

                foreach (var contact in trace.Contacts.Where(c => owner[c.PointerId] == target))
                {
                    alone
                        .Of(contact.PointerId)
                        .ShouldBe(
                            full.Of(contact.PointerId),
                            "contacts on other targets must not change what this contact produces"
                        );
                }
            },
            iter: Iterations,
            print: t => t.ToString()
        );

    [Fact]
    public void Nothing_is_reported_for_a_contact_after_it_was_cancelled_or_lifted() =>
        TouchTraceGen.Trace.Sample(
            trace =>
            {
                var run = trace.Run();
                foreach (var contact in trace.Contacts)
                {
                    var end = trace.Steps.IndexOf(
                        trace.Steps.First(s =>
                            s.Frame is { } frame
                            && frame.Samples.Any(sample =>
                                sample.PointerId == contact.PointerId
                                && sample.Phase is PointerPhase.Up or PointerPhase.Cancel
                            )
                        )
                    );
                    run.Events.Skip(run.Marks[end])
                        .ShouldNotContain(e => e.PointerId == contact.PointerId);
                    if (contact.Canceled)
                    {
                        run.Of(contact.PointerId)
                            .ShouldNotContain(e =>
                                e.Kind == GestureKind.Tap || e.Kind == GestureKind.Swipe
                            );
                    }
                }
            },
            iter: Iterations,
            print: t => t.ToString()
        );

    [Fact]
    [Trait("Req", "EJE-004")]
    [Trait("Req", "REG-03")]
    public void Every_hold_that_starts_ends_exactly_once_and_after_it_started() =>
        TouchTraceGen.Trace.Sample(
            trace =>
            {
                var run = trace.Run();
                run.ContactsAfterReset.ShouldBe(0);
                foreach (var contact in trace.Contacts)
                {
                    var events = run.Of(contact.PointerId).ToList();
                    var starts = events.FindAll(e => e.Kind == GestureKind.HoldStart);
                    var ends = events.FindAll(e => e.Kind == GestureKind.HoldEnd);
                    starts.Count.ShouldBeLessThanOrEqualTo(1);
                    ends.Count.ShouldBe(starts.Count);
                    if (starts.Count == 1)
                    {
                        events.IndexOf(ends[0]).ShouldBeGreaterThan(events.IndexOf(starts[0]));
                        ends[0].Timestamp.ShouldBeGreaterThanOrEqualTo(starts[0].Timestamp);
                    }
                }
            },
            iter: Iterations,
            print: t => t.ToString()
        );

    [Fact]
    public void Accepted_touches_on_one_target_are_never_closer_than_the_debounce() =>
        TouchTraceGen.QuietTrace.Sample(
            trace =>
            {
                var accepted = trace
                    .Run()
                    .Events.Where(e => e.Kind is GestureKind.Tap or GestureKind.HoldStart)
                    .GroupBy(e => e.Target);
                foreach (var target in accepted)
                {
                    var times = target.Select(e => e.Timestamp).Order().ToList();
                    for (var i = 1; i < times.Count; i++)
                    {
                        (times[i] - times[i - 1]).ShouldBeGreaterThanOrEqualTo(
                            trace.Settings.Debounce
                        );
                    }
                }
            },
            iter: Iterations,
            print: t => t.ToString()
        );

    [Fact]
    [Trait("Req", "ACC-007")]
    [Trait("Req", "REG-02")]
    public void Only_fingers_that_pass_the_filter_act_and_only_on_the_target_they_went_down_on() =>
        TouchTraceGen.Trace.Sample(
            trace =>
            {
                var slop = HitSlop(trace);
                var run = trace.Run();
                foreach (var contact in trace.Contacts)
                {
                    var acted = run.Of(contact.PointerId)
                        .Where(e =>
                            e.Kind
                                is GestureKind.Tap
                                    or GestureKind.LongPress
                                    or GestureKind.HoldStart
                                    or GestureKind.Swipe
                        )
                        .ToList();
                    if (contact.ContactSize == TouchTraceGen.PalmSize)
                    {
                        acted.ShouldBeEmpty("a palm never acts");
                        continue;
                    }

                    var hit = HitResolver.Resolve(trace.Targets.AsSpan(), contact.Start, slop);
                    foreach (var gesture in acted.Where(e => e.Kind != GestureKind.Swipe))
                    {
                        hit.ShouldBeGreaterThanOrEqualTo(0);
                        gesture.Target.ShouldBe(trace.Targets[hit].Id);
                    }

                    foreach (var tap in acted.Where(e => e.Kind == GestureKind.Tap))
                    {
                        (
                            tap.Timestamp - TouchScript.At(contact.StartMs)
                        ).ShouldBeGreaterThanOrEqualTo(trace.Settings.MinContact);
                        if (trace.Settings.CancelMovePx > 0)
                        {
                            (MaxDisplacement(contact) / trace.DpiScale).ShouldBeLessThanOrEqualTo(
                                trace.Settings.CancelMovePx
                            );
                        }
                    }
                }
            },
            iter: Iterations,
            print: t => t.ToString()
        );

    [Fact]
    public void Gestures_come_in_time_order_and_the_same_trace_always_gives_the_same_gestures() =>
        TouchTraceGen.Trace.Sample(
            trace =>
            {
                var first = trace.Run();
                var times = first.Events.Take(first.BeforeReset).Select(e => e.Timestamp).ToList();
                times.ShouldBe(times.Order().ToList());
                trace.Run().Events.ShouldBe(first.Events);
            },
            iter: Iterations,
            print: t => t.ToString()
        );

    private static int HitSlop(TouchTrace trace) =>
        (int)Math.Round(trace.Settings.HitSlopPx * trace.DpiScale, MidpointRounding.AwayFromZero);

    private static double MaxDisplacement(ContactPlan contact)
    {
        var at = contact.Start;
        var max = 0d;
        foreach (var (_, dx, dy) in contact.Moves)
        {
            at = new PhysicalPoint(at.X + dx, at.Y + dy);
            max = Math.Max(max, contact.Start.DistanceTo(at));
        }

        return max;
    }
}
