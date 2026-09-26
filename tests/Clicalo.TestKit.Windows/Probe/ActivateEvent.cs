using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_ACTIVATE</c>: the probe window was activated or deactivated.</summary>
public sealed class ActivateEvent : ProbeMessageEvent
{
    internal ActivateEvent(JsonElement json, string line)
        : base(json, line)
    {
        State = (ActivationState)JsonFields.Int32(json, ProbeFields.State);
        IsMinimized = JsonFields.Boolean(json, ProbeFields.Minimized);
        OtherWindow = JsonFields.Handle(json, ProbeFields.OtherWindow);
    }

    /// <summary>Activated (and how) or deactivated.</summary>
    public ActivationState State { get; }

    /// <summary>The window is minimized.</summary>
    public bool IsMinimized { get; }

    /// <summary>The window being deactivated or activated in exchange (zero when it belongs to another thread).</summary>
    public nint OtherWindow { get; }
}
