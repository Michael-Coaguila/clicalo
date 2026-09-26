using System.Text.Json;
using Clicalo.TestKit.Windows.Input;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_INPUT</c> from a keyboard: the exact make code and prefix flags, before any translation.</summary>
public sealed class RawKeyboardEvent : ProbeMessageEvent
{
    private const ushort BreakFlag = 0x01;
    private const ushort E0Flag = 0x02;
    private const ushort E1Flag = 0x04;

    internal RawKeyboardEvent(JsonElement json, string line)
        : base(json, line)
    {
        MakeCode = JsonFields.UInt16(json, ProbeFields.ScanCode);
        Flags = JsonFields.UInt16(json, ProbeFields.RawFlags);
        VirtualKey = (VirtualKeyCode)JsonFields.UInt16(json, ProbeFields.VirtualKey);
        RawMessage = JsonFields.UInt32(json, ProbeFields.RawMessage);
        Device = JsonFields.Handle(json, ProbeFields.Device);
        IsSink = JsonFields.Boolean(json, ProbeFields.Sink);
    }

    /// <summary><c>RAWKEYBOARD.MakeCode</c>: the scan code without its prefix.</summary>
    public ushort MakeCode { get; }

    /// <summary><c>RAWKEYBOARD.Flags</c>.</summary>
    public ushort Flags { get; }

    /// <summary><c>RAWKEYBOARD.VKey</c> (generic for Shift, Ctrl and Alt).</summary>
    public VirtualKeyCode VirtualKey { get; }

    /// <summary><c>RAWKEYBOARD.Message</c> (<c>WM_KEYDOWN</c>, <c>WM_SYSKEYUP</c>...).</summary>
    public uint RawMessage { get; }

    /// <summary><c>RAWINPUTHEADER.hDevice</c>: zero for injected input.</summary>
    public nint Device { get; }

    /// <summary>True when delivered while the probe was not in the foreground (never expected: no INPUTSINK).</summary>
    public bool IsSink { get; }

    /// <summary><c>RI_KEY_BREAK</c>: a release.</summary>
    public bool IsBreak => (Flags & BreakFlag) != 0;

    /// <summary><c>RI_KEY_E0</c>: the extended prefix (right Ctrl and Alt, arrows...).</summary>
    public bool IsE0 => (Flags & E0Flag) != 0;

    /// <summary><c>RI_KEY_E1</c>: the Pause prefix.</summary>
    public bool IsE1 => (Flags & E1Flag) != 0;
}
