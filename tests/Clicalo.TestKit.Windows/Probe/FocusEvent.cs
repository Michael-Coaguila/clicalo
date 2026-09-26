using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_SETFOCUS</c> or <c>WM_KILLFOCUS</c>.</summary>
public sealed class FocusEvent : ProbeMessageEvent
{
    internal FocusEvent(JsonElement json, string line)
        : base(json, line)
    {
        IsGained = JsonFields.Boolean(json, ProbeFields.Gained);
        OtherWindow = JsonFields.Handle(json, ProbeFields.OtherWindow);
    }

    /// <summary>True for <c>WM_SETFOCUS</c>, false for <c>WM_KILLFOCUS</c>.</summary>
    public bool IsGained { get; }

    /// <summary>The window that lost (for set) or receives (for kill) the keyboard focus; may be zero.</summary>
    public nint OtherWindow { get; }
}
