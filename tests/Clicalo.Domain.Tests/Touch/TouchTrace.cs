using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// A generated pointer trace for one surface: the recognizer's settings and targets, and the steps (frames and timer
/// ticks) in time order. Built by <see cref="TouchTraceGen"/> for the property tests.
/// </summary>
/// <param name="Settings">Filter values of the surface.</param>
/// <param name="DpiScale">Scale of its monitor.</param>
/// <param name="Targets">Its targets, in physical pixels.</param>
/// <param name="Steps">Frames and ticks, in time order.</param>
/// <param name="Contacts">What each contact did, by pointer identifier (every contact has its own).</param>
internal sealed record TouchTrace(
    TouchSettings Settings,
    double DpiScale,
    ImmutableArray<TouchTarget> Targets,
    ImmutableArray<TraceStep> Steps,
    ImmutableArray<ContactPlan> Contacts
)
{
    /// <summary>The timer ticks of the trace, in milliseconds from its start.</summary>
    public ImmutableArray<int> Ticks { get; init; } = [];

    /// <summary>Feeds every step to a new recognizer.</summary>
    public TraceRun Run() => Run(Steps);

    /// <summary>Feeds <paramref name="steps"/> to a new recognizer with this trace's settings and targets.</summary>
    public TraceRun Run(ImmutableArray<TraceStep> steps)
    {
        var recognizer = new GestureRecognizer(Settings, DpiScale);
        recognizer.SetTargets(Targets);
        var events = new List<GestureEvent>();
        var marks = new int[steps.Length];
        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            if (step.Frame is { } frame)
            {
                recognizer.Feed(frame, events);
            }
            else
            {
                recognizer.OnTick(step.At, events);
            }

            marks[i] = events.Count;
        }

        var beforeReset = events.Count;
        var end = steps.IsEmpty ? DateTimeOffset.MinValue : steps[^1].At;
        recognizer.Reset(end + TimeSpan.FromSeconds(10), events);
        return new TraceRun(events, [.. marks], beforeReset, recognizer.ActiveContacts);
    }

    /// <summary>A compact description for a failing sample.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"settings {Settings}, dpi {DpiScale}\n");
        foreach (var target in Targets)
        {
            text.Append(CultureInfo.InvariantCulture, $"  target {target}\n");
        }

        foreach (var step in Steps)
        {
            var ms = (step.At - TouchScript.At(0)).TotalMilliseconds;
            if (step.Frame is { } frame)
            {
                foreach (var sample in frame.Samples)
                {
                    text.Append(
                        CultureInfo.InvariantCulture,
                        $"  {ms} ms #{sample.PointerId} {sample.Phase} {sample.Position.X},{sample.Position.Y} contact {sample.Contact.Width}\n"
                    );
                }
            }
            else
            {
                text.Append(CultureInfo.InvariantCulture, $"  {ms} ms tick\n");
            }
        }

        return text.ToString();
    }
}
