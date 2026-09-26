using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_UNICHAR</c> carrying a UTF-32 code point (sent by some IMEs and applications, never by SendInput).</summary>
public sealed class UniCharMessageEvent : ProbeMessageEvent
{
    internal UniCharMessageEvent(JsonElement json, string line)
        : base(json, line)
    {
        CodePoint = JsonFields.UInt32(json, ProbeFields.CodePoint);
        Text = JsonFields.OptionalString(json, ProbeFields.Text);
    }

    /// <summary>The UTF-32 code point.</summary>
    public uint CodePoint { get; }

    /// <summary>The character, or null when the code point is not a valid scalar value.</summary>
    public string? Text { get; }
}
