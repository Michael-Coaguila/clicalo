using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>A mouse button down, up or double-click message, with client-area coordinates.</summary>
public sealed class MouseButtonEvent : ProbeMessageEvent
{
    internal MouseButtonEvent(JsonElement json, string line)
        : base(json, line)
    {
        Button = JsonFields.String(json, ProbeFields.Button) switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            "x1" => MouseButton.X1,
            "x2" => MouseButton.X2,
            var other => throw new FormatException("Unknown mouse button '" + other + "'."),
        };
        IsDown = JsonFields.Boolean(json, ProbeFields.Down);
        IsDoubleClick = JsonFields.Boolean(json, ProbeFields.DoubleClick);
        X = JsonFields.Int32(json, ProbeFields.X);
        Y = JsonFields.Int32(json, ProbeFields.Y);
        KeyFlags = JsonFields.UInt16(json, ProbeFields.KeyFlags);
    }

    /// <summary>The button.</summary>
    public MouseButton Button { get; }

    /// <summary>True for down and double-click messages.</summary>
    public bool IsDown { get; }

    /// <summary>True for double-click messages.</summary>
    public bool IsDoubleClick { get; }

    /// <summary>Horizontal client coordinate, in physical pixels (the probe is per-monitor DPI aware).</summary>
    public int X { get; }

    /// <summary>Vertical client coordinate, in physical pixels.</summary>
    public int Y { get; }

    /// <summary><c>MK_*</c> flags.</summary>
    public ushort KeyFlags { get; }
}
