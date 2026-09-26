using System.Collections.Immutable;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Time;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// Drives one <see cref="GestureRecognizer"/> with a readable script of contacts, in milliseconds from
/// <see cref="TestTime.Epoch"/> and physical pixels, and collects every gesture it reports.
/// </summary>
internal sealed class TouchScript
{
    /// <summary>Side of a fingertip contact area, in physical pixels.</summary>
    public const int FingerSize = 12;

    private readonly List<GestureEvent> _events = [];
    private readonly Dictionary<uint, PhysicalPoint> _positions = [];

    public TouchScript(TouchSettings settings, params TouchTarget[] targets)
        : this(settings, 1.0, targets) { }

    public TouchScript(TouchSettings settings, double dpiScale, params TouchTarget[] targets)
    {
        Recognizer = new GestureRecognizer(settings, dpiScale);
        Recognizer.SetTargets([.. targets]);
    }

    public GestureRecognizer Recognizer { get; }

    /// <summary>Every gesture reported so far, in order.</summary>
    public IReadOnlyList<GestureEvent> Events => _events;

    /// <summary>The kinds of <see cref="Events"/>, for compact assertions.</summary>
    public IReadOnlyList<GestureKind> Kinds => [.. _events.Select(e => e.Kind)];

    /// <summary>The instant <paramref name="milliseconds"/> after the epoch.</summary>
    public static DateTimeOffset At(double milliseconds) =>
        TestTime.Epoch + TimeSpan.FromMilliseconds(milliseconds);

    /// <summary>A target of <paramref name="kind"/> with its top-left corner at (x, y).</summary>
    public static TouchTarget Tile(
        int id,
        int x,
        int y,
        int width = 80,
        int height = 80,
        TouchTargetKind kind = TouchTargetKind.TapOrLongPress
    ) => new(new TouchTargetId(id), new PhysicalRect(x, y, width, height), kind);

    /// <summary>A contact sample; <paramref name="contactSize"/> is the side of its square contact area.</summary>
    public static PointerSample Sample(
        uint id,
        PointerPhase phase,
        int x,
        int y,
        double atMs,
        int contactSize = FingerSize
    ) =>
        new(
            id,
            PointerKind.Finger,
            phase,
            new PhysicalPoint(x, y),
            new PhysicalRect(
                x - (contactSize / 2),
                y - (contactSize / 2),
                contactSize,
                contactSize
            ),
            At(atMs),
            PointerInputOrigin.Hardware
        );

    public TouchScript Down(uint id, int x, int y, double atMs, int contactSize = FingerSize)
    {
        _positions[id] = new PhysicalPoint(x, y);
        return Frame(atMs, Sample(id, PointerPhase.Down, x, y, atMs, contactSize));
    }

    public TouchScript Move(uint id, int x, int y, double atMs, int contactSize = FingerSize)
    {
        _positions[id] = new PhysicalPoint(x, y);
        return Frame(atMs, Sample(id, PointerPhase.Move, x, y, atMs, contactSize));
    }

    /// <summary>Lifts the contact where it last was.</summary>
    public TouchScript Up(uint id, double atMs)
    {
        var at = _positions[id];
        return Up(id, at.X, at.Y, atMs);
    }

    public TouchScript Up(uint id, int x, int y, double atMs)
    {
        _positions[id] = new PhysicalPoint(x, y);
        return Frame(atMs, Sample(id, PointerPhase.Up, x, y, atMs));
    }

    public TouchScript Cancel(uint id, double atMs)
    {
        var at = _positions[id];
        return Frame(atMs, Sample(id, PointerPhase.Cancel, at.X, at.Y, atMs));
    }

    /// <summary>A down and an up at the same point, <paramref name="durationMs"/> apart.</summary>
    public TouchScript Tap(uint id, int x, int y, double atMs, double durationMs = 40) =>
        Down(id, x, y, atMs).Up(id, atMs + durationMs);

    /// <summary>Feeds one frame with <paramref name="samples"/> at <paramref name="atMs"/>.</summary>
    public TouchScript Frame(double atMs, params PointerSample[] samples)
    {
        var frame = new PointerFrame(1, At(atMs), [.. samples]);
        Recognizer.Feed(frame, _events);
        return this;
    }

    public TouchScript Tick(double atMs)
    {
        Recognizer.OnTick(At(atMs), _events);
        return this;
    }

    public TouchScript Reset(double atMs)
    {
        Recognizer.Reset(At(atMs), _events);
        return this;
    }

    public TouchScript SetTargets(params TouchTarget[] targets)
    {
        Recognizer.SetTargets([.. targets]);
        return this;
    }

    /// <summary>Forgets the gestures reported so far.</summary>
    public TouchScript ClearEvents()
    {
        _events.Clear();
        return this;
    }

    /// <summary>The single reported gesture; fails if there is not exactly one.</summary>
    public GestureEvent Single() => _events.ShouldHaveSingleItem();

    /// <summary>The targets of a batch of contacts, for data-driven layouts.</summary>
    public static ImmutableArray<TouchTarget> Row(
        int count,
        int x,
        int y,
        int size,
        int gap,
        TouchTargetKind kind
    ) =>
        [
            .. Enumerable
                .Range(0, count)
                .Select(i => Tile(i + 1, x + (i * (size + gap)), y, size, size, kind)),
        ];
}
