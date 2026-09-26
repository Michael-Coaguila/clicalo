using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>Something failed inside the probe (it keeps running). Sessions fail the waiting test when one arrives.</summary>
public sealed class ProbeErrorEvent : ProbeEvent
{
    internal ProbeErrorEvent(JsonElement json, string line)
        : base(json, line)
    {
        Detail = JsonFields.String(json, ProbeFields.Detail);
    }

    /// <summary>What failed.</summary>
    public string Detail { get; }
}
