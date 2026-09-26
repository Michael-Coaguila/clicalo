using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>One event written by InputProbe (one JSON line), parsed by <see cref="ProbeEventParser"/>.</summary>
public abstract class ProbeEvent
{
    private protected ProbeEvent(JsonElement json, string line)
    {
        Kind = JsonFields.String(json, ProbeFields.Kind);
        Sequence = JsonFields.Int64(json, ProbeFields.Sequence);
        Timestamp = JsonFields.Int64(json, ProbeFields.Timestamp);
        ForegroundWindow = JsonFields.Handle(json, ProbeFields.ForegroundWindow);
        Json = line;
    }

    /// <summary>Event type as written by the probe (for example <c>key</c>).</summary>
    public string Kind { get; }

    /// <summary>Order in which the probe handled the event, from 1 and without gaps.</summary>
    public long Sequence { get; }

    /// <summary>QueryPerformanceCounter ticks when the probe handled it (see <see cref="ProbeReadyEvent.TimestampFrequency"/>).</summary>
    public long Timestamp { get; }

    /// <summary><c>GetForegroundWindow</c> at that moment.</summary>
    public nint ForegroundWindow { get; }

    /// <summary>The original line, the most complete description of the event.</summary>
    public string Json { get; }

    public override string ToString() => Json;
}
