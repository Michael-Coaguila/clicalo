using System.Text.Json;
using Clicalo.TestKit.Windows.Input;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_KEYDOWN</c>, <c>WM_KEYUP</c>, <c>WM_SYSKEYDOWN</c> or <c>WM_SYSKEYUP</c> as the application saw it.</summary>
public sealed class KeyMessageEvent : ProbeMessageEvent
{
    private const uint SystemKeyDown = 0x0104;
    private const uint SystemKeyUp = 0x0105;

    internal KeyMessageEvent(JsonElement json, string line)
        : base(json, line)
    {
        VirtualKey = (VirtualKeyCode)JsonFields.UInt16(json, ProbeFields.VirtualKey);
        SideVirtualKey = (VirtualKeyCode)JsonFields.UInt16(json, ProbeFields.SideVirtualKey);
        ScanCode = (byte)JsonFields.UInt16(json, ProbeFields.ScanCode);
        IsExtended = JsonFields.Boolean(json, ProbeFields.Extended);
        RepeatCount = JsonFields.UInt16(json, ProbeFields.RepeatCount);
        IsAltDown = JsonFields.Boolean(json, ProbeFields.AltDown);
        WasDown = JsonFields.Boolean(json, ProbeFields.WasDown);
        IsRelease = JsonFields.Boolean(json, ProbeFields.Released);
        Modifiers = (SideModifiers)JsonFields.Int32(json, ProbeFields.Modifiers);
    }

    /// <summary>The <c>wParam</c> key: generic for Shift, Ctrl and Alt (<see cref="VirtualKeyCode.Control"/>...).</summary>
    public VirtualKeyCode VirtualKey { get; }

    /// <summary>The side-specific key (<see cref="VirtualKeyCode.RightControl"/>...), from the scan code and extended flag.</summary>
    public VirtualKeyCode SideVirtualKey { get; }

    /// <summary>Scan code from <c>lParam</c> (bits 16–23).</summary>
    public byte ScanCode { get; }

    /// <summary>Extended-key flag from <c>lParam</c> (bit 24): right Ctrl and Alt, arrows, navigation keys...</summary>
    public bool IsExtended { get; }

    /// <summary>Repeat count from <c>lParam</c>.</summary>
    public ushort RepeatCount { get; }

    /// <summary>Context code: Alt was down.</summary>
    public bool IsAltDown { get; }

    /// <summary>Previous key state: the key was already down (auto-repeat).</summary>
    public bool WasDown { get; }

    /// <summary>True for a key release (<c>WM_KEYUP</c>/<c>WM_SYSKEYUP</c>).</summary>
    public bool IsRelease { get; }

    /// <summary>True for a key press (<c>WM_KEYDOWN</c>/<c>WM_SYSKEYDOWN</c>).</summary>
    public bool IsPress => !IsRelease;

    /// <summary>True for <c>WM_SYSKEYDOWN</c>/<c>WM_SYSKEYUP</c> (Alt held, or F10).</summary>
    public bool IsSystemKey => Message is SystemKeyDown or SystemKeyUp;

    /// <summary>Side-specific modifiers down while the message was handled (after this key's own transition).</summary>
    public SideModifiers Modifiers { get; }
}
