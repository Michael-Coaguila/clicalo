using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>Outcome of the probe calling <c>SetForegroundWindow</c> on request.</summary>
public sealed class ProbeForegroundEvent : ProbeEvent
{
    internal ProbeForegroundEvent(JsonElement json, string line)
        : base(json, line)
    {
        Id = JsonFields.OptionalInt64(json, ProbeFields.Id);
        Window = JsonFields.Handle(json, ProbeFields.Window);
        Succeeded = JsonFields.Boolean(json, ProbeFields.Succeeded);
    }

    /// <summary>The identifier of the command.</summary>
    public long? Id { get; }

    /// <summary>The window the probe tried to bring to the foreground.</summary>
    public nint Window { get; }

    /// <summary>What <c>SetForegroundWindow</c> returned (Windows may refuse without an error).</summary>
    public bool Succeeded { get; }
}
