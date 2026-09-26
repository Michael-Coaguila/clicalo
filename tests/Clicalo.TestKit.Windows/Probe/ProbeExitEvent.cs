using System.Text.Json;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>The probe window is being destroyed; no event follows.</summary>
public sealed class ProbeExitEvent : ProbeEvent
{
    internal ProbeExitEvent(JsonElement json, string line)
        : base(json, line) { }
}
