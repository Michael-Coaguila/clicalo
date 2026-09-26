using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_MOUSEWHEEL</c> or <c>WM_MOUSEHWHEEL</c>, with screen coordinates.</summary>
public sealed class MouseWheelEvent : ProbeMessageEvent
{
    internal MouseWheelEvent(JsonElement json, string line)
        : base(json, line)
    {
        IsHorizontal = JsonFields.Boolean(json, ProbeFields.Horizontal);
        Delta = JsonFields.Int32(json, ProbeFields.Delta);
        X = JsonFields.Int32(json, ProbeFields.X);
        Y = JsonFields.Int32(json, ProbeFields.Y);
        KeyFlags = JsonFields.UInt16(json, ProbeFields.KeyFlags);
    }

    /// <summary>True for the horizontal wheel.</summary>
    public bool IsHorizontal { get; }

    /// <summary>Signed delta: positive is away from the user (vertical) or to the right (horizontal).</summary>
    public int Delta { get; }

    /// <summary>Horizontal screen coordinate, in physical pixels.</summary>
    public int X { get; }

    /// <summary>Vertical screen coordinate, in physical pixels.</summary>
    public int Y { get; }

    /// <summary><c>MK_*</c> flags.</summary>
    public ushort KeyFlags { get; }
}
