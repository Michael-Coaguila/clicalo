using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_ACTIVATEAPP</c>: the probe application was activated or deactivated.</summary>
public sealed class AppActivateEvent : ProbeMessageEvent
{
    internal AppActivateEvent(JsonElement json, string line)
        : base(json, line)
    {
        IsActive = JsonFields.Boolean(json, ProbeFields.Active);
        OtherThreadId = JsonFields.UInt32(json, ProbeFields.OtherThread);
    }

    /// <summary>True when the probe is being activated.</summary>
    public bool IsActive { get; }

    /// <summary>Thread of the application losing or gaining activation in exchange.</summary>
    public uint OtherThreadId { get; }
}
