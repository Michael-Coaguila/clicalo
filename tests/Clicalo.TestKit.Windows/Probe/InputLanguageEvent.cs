using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_INPUTLANGCHANGE</c>: the keyboard layout of the probe thread changed.</summary>
public sealed class InputLanguageEvent : ProbeMessageEvent
{
    internal InputLanguageEvent(JsonElement json, string line)
        : base(json, line)
    {
        CharSet = JsonFields.UInt64(json, ProbeFields.CharSet);
        KeyboardLayout = JsonFields.Handle(json, ProbeFields.KeyboardLayout);
    }

    /// <summary>Character set of the new layout.</summary>
    public ulong CharSet { get; }

    /// <summary>The new keyboard layout (<c>HKL</c>).</summary>
    public nint KeyboardLayout { get; }
}
