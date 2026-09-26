using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>Answer to a ping: every command sent before it has been handled.</summary>
public sealed class ProbePongEvent : ProbeEvent
{
    internal ProbePongEvent(JsonElement json, string line)
        : base(json, line)
    {
        Id = JsonFields.OptionalInt64(json, ProbeFields.Id);
    }

    /// <summary>The identifier of the ping.</summary>
    public long? Id { get; }
}
