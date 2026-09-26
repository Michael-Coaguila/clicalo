using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>The probe window exists, Raw Input is registered and commands are accepted.</summary>
public sealed class ProbeReadyEvent : ProbeEvent
{
    internal ProbeReadyEvent(JsonElement json, string line)
        : base(json, line)
    {
        Protocol = JsonFields.Int32(json, ProbeFields.Protocol);
        Window = JsonFields.Handle(json, ProbeFields.Window);
        ProcessId = JsonFields.Int32(json, ProbeFields.ProcessId);
        ThreadId = JsonFields.UInt32(json, ProbeFields.ThreadId);
        SessionId = JsonFields.Int32(json, ProbeFields.Session);
        TimestampFrequency = JsonFields.Int64(json, ProbeFields.TimestampFrequency);
        KeyboardLayout = JsonFields.Handle(json, ProbeFields.KeyboardLayout);
    }

    /// <summary>Wire protocol version of the probe.</summary>
    public int Protocol { get; }

    /// <summary>The probe window (<c>HWND</c>): the only legitimate injection target.</summary>
    public nint Window { get; }

    /// <summary>Process identifier of the probe.</summary>
    public int ProcessId { get; }

    /// <summary>Thread that owns the probe window.</summary>
    public uint ThreadId { get; }

    /// <summary>Session of the probe; 0 means a service session without an interactive desktop.</summary>
    public int SessionId { get; }

    /// <summary>QueryPerformanceCounter ticks per second, to turn <see cref="ProbeEvent.Timestamp"/> into time.</summary>
    public long TimestampFrequency { get; }

    /// <summary>The keyboard layout (<c>HKL</c>) of the probe thread at start-up.</summary>
    public nint KeyboardLayout { get; }
}
