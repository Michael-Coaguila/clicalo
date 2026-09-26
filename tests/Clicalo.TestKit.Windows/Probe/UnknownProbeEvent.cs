using System.Text.Json;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>An event kind this version of the TestKit does not know (a newer probe); kept for diagnostics.</summary>
public sealed class UnknownProbeEvent : ProbeEvent
{
    internal UnknownProbeEvent(JsonElement json, string line)
        : base(json, line) { }
}
