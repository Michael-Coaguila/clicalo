using Clicalo.Domain.Touch;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// The sink of a test surface: records every frame (copying its samples, which the source reuses), every hover change
/// and every gesture, and forwards the frames to the surface's <see cref="GestureHost"/>. Written on the UI thread,
/// read from the test thread.
/// </summary>
public sealed class PointerRecorder : IPointerFrameSink
{
    private readonly Lock _gate = new();
    private readonly List<RecordedFrame> _frames = [];
    private readonly List<RecordedGesture> _gestures = [];
    private readonly List<bool> _hovers = [];

    public PointerRecorder(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Clock = clock;
    }

    public TimeProvider Clock { get; }

    /// <summary>Receives the frames after they are recorded; null records only.</summary>
    public GestureHost? Host { get; set; }

    public IReadOnlyList<RecordedFrame> Frames
    {
        get
        {
            lock (_gate)
            {
                return [.. _frames];
            }
        }
    }

    public IReadOnlyList<RecordedGesture> Gestures
    {
        get
        {
            lock (_gate)
            {
                return [.. _gestures];
            }
        }
    }

    public IReadOnlyList<bool> Hovers
    {
        get
        {
            lock (_gate)
            {
                return [.. _hovers];
            }
        }
    }

    /// <summary>Every sample recorded so far, in order.</summary>
    public IReadOnlyList<PointerSample> Samples => [.. Frames.SelectMany(f => f.Frame.Samples)];

    public void OnFrame(in PointerFrame frame)
    {
        var copy = frame with { Samples = [.. frame.Samples] };
        lock (_gate)
        {
            _frames.Add(new RecordedFrame(copy, Clock.GetUtcNow()));
        }

        Host?.OnFrame(frame);
    }

    public void OnHover(bool inside)
    {
        lock (_gate)
        {
            _hovers.Add(inside);
        }

        Host?.OnHover(inside);
    }

    /// <summary>The gesture handler to give to the <see cref="GestureHost"/>.</summary>
    public void OnGesture(GestureEvent gesture)
    {
        var received = new RecordedGesture(
            gesture,
            Clock.GetUtcNow(),
            Environment.CurrentManagedThreadId
        );
        lock (_gate)
        {
            _gestures.Add(received);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _frames.Clear();
            _gestures.Clear();
            _hovers.Clear();
        }
    }
}
