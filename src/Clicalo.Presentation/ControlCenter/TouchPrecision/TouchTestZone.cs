using System.Collections.Immutable;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;

namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>
/// The test zone of «Precisión táctil» (TAC-006): several «Toca aquí» targets separated by a gap, judged by THE SAME
/// <see cref="GestureRecognizer"/> and <see cref="TouchFilter"/> as the panel (TAC-002), so a touch in the gap within
/// the extra area counts for the nearest target and each target keeps its own debounce. It counts the registered and
/// ignored touches and remembers the last one. Pure: the view feeds it contacts in physical screen pixels and the
/// caller stamps them with its clock.
/// </summary>
public sealed class TouchTestZone
{
    /// <summary>How many «Toca aquí» targets the zone has.</summary>
    public const int TargetCount = 2;

    private readonly GestureRecognizer _recognizer;
    private readonly List<GestureEvent> _events = new(capacity: 4);
    private readonly TestMark[] _marks = new TestMark[TargetCount];
    private uint _frame;

    /// <summary>Creates the zone with the filter values in use.</summary>
    /// <param name="settings">The touch filter values.</param>
    public TouchTestZone(TouchSettings settings) =>
        _recognizer = new GestureRecognizer(settings, 1);

    /// <summary>Touches that passed the filter.</summary>
    public int Registered { get; private set; }

    /// <summary>Touches on a target that the filter ignored.</summary>
    public int Ignored { get; private set; }

    /// <summary>What happened to the last touch; <see langword="null"/> before the first one.</summary>
    public TestOutcome? Last { get; private set; }

    /// <summary>The mark of each target: the last touch it received.</summary>
    public ImmutableArray<TestMark> Marks => [.. _marks];

    /// <summary>The filter values in use.</summary>
    public TouchSettings Settings => _recognizer.Settings;

    /// <summary>New filter values: the next touch uses them (a contact already down finishes with the old ones).</summary>
    /// <param name="settings">The touch filter values.</param>
    public void Configure(TouchSettings settings)
    {
        if (settings != _recognizer.Settings)
        {
            _recognizer.Configure(settings, _recognizer.DpiScale);
        }
    }

    /// <summary>Where the targets are now, in physical screen pixels, and the scale of their monitor.</summary>
    /// <param name="bounds">The bounds of each target, <see cref="TargetCount"/> of them.</param>
    /// <param name="dpiScale">Physical pixels per logical pixel.</param>
    public void SetTargets(IReadOnlyList<PhysicalRect> bounds, double dpiScale)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (dpiScale > 0 && Math.Abs(dpiScale - _recognizer.DpiScale) > double.Epsilon)
        {
            _recognizer.Configure(_recognizer.Settings, dpiScale);
        }

        _recognizer.SetTargets([
            .. bounds
                .Take(TargetCount)
                .Select(
                    (rect, i) => new TouchTarget(new TouchTargetId(i), rect, TouchTargetKind.Tap)
                ),
        ]);
    }

    /// <summary>A contact went down, moved, lifted or was cancelled.</summary>
    /// <param name="pointerId">The contact.</param>
    /// <param name="phase">What it did.</param>
    /// <param name="at">Where, in physical screen pixels.</param>
    /// <param name="now">When, on the caller's clock (never backwards).</param>
    /// <returns>Whether a touch was judged (the counters or the last touch changed).</returns>
    public bool Feed(uint pointerId, PointerPhase phase, PhysicalPoint at, DateTimeOffset now)
    {
        _events.Clear();
        var sample = new PointerSample(
            pointerId,
            PointerKind.Mouse,
            phase,
            at,
            new PhysicalRect(at.X, at.Y, 1, 1),
            now,
            PointerInputOrigin.Unknown
        );
        _recognizer.Feed(new PointerFrame(++_frame, now, [sample]), _events);
        var judged = false;
        foreach (var gesture in _events)
        {
            judged |= Judge(gesture);
        }

        return judged;
    }

    /// <summary>[testReset]: the counters, the marks and the last touch start again.</summary>
    public void Reset()
    {
        Registered = 0;
        Ignored = 0;
        Last = null;
        Array.Clear(_marks);
    }

    private bool Judge(GestureEvent gesture)
    {
        TestOutcome outcome;
        switch (gesture.Kind)
        {
            case GestureKind.Tap:
                outcome = TestOutcome.Registered;
                break;
            case GestureKind.Swipe:
                outcome = TestOutcome.Moved;
                break;
            case GestureKind.Ignored when gesture.Target is not null:
                switch (gesture.Ignored)
                {
                    case IgnoreReason.TooShort:
                        outcome = TestOutcome.TooShort;
                        break;
                    case IgnoreReason.Debounced:
                        outcome = TestOutcome.TooSoon;
                        break;
                    case IgnoreReason.Moved or IgnoreReason.SwipeLock or IgnoreReason.Palm:
                        outcome = TestOutcome.Moved;
                        break;
                    default:
                        // Outside every target and its extra area, or cancelled by the system: not a touch of the zone.
                        return false;
                }

                break;
            default:
                return false;
        }

        if (outcome == TestOutcome.Registered)
        {
            Registered++;
        }
        else
        {
            Ignored++;
        }

        Last = outcome;
        if (gesture.Target is { Value: >= 0 and < TargetCount } target)
        {
            _marks[target.Value] =
                outcome == TestOutcome.Registered ? TestMark.Registered : TestMark.Ignored;
        }

        return true;
    }
}
