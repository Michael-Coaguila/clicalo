using System.Text.Json;
using Clicalo.TestKit.Windows.Input;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary><c>WM_CHAR</c>, <c>WM_SYSCHAR</c>, <c>WM_DEADCHAR</c> or <c>WM_SYSDEADCHAR</c>.</summary>
public sealed class CharMessageEvent : ProbeMessageEvent
{
    private const uint CharMessage = 0x0102;

    internal CharMessageEvent(JsonElement json, string line)
        : base(json, line)
    {
        CodeUnit = (char)JsonFields.UInt16(json, ProbeFields.CodeUnit);
        Text = JsonFields.OptionalString(json, ProbeFields.Text);
        ScanCode = (byte)JsonFields.UInt16(json, ProbeFields.ScanCode);
        IsExtended = JsonFields.Boolean(json, ProbeFields.Extended);
        RepeatCount = JsonFields.UInt16(json, ProbeFields.RepeatCount);
        Modifiers = (SideModifiers)JsonFields.Int32(json, ProbeFields.Modifiers);
    }

    /// <summary>The UTF-16 code unit in <c>wParam</c>.</summary>
    public char CodeUnit { get; }

    /// <summary>
    /// The complete character this message finishes, or null while the high half of a surrogate pair waits for
    /// its low half (an emoji arrives as two <c>WM_CHAR</c>; the second carries both units).
    /// </summary>
    public string? Text { get; }

    /// <summary>Scan code from <c>lParam</c>.</summary>
    public byte ScanCode { get; }

    /// <summary>Extended-key flag from <c>lParam</c>.</summary>
    public bool IsExtended { get; }

    /// <summary>Repeat count from <c>lParam</c>.</summary>
    public ushort RepeatCount { get; }

    /// <summary>Side-specific modifiers down while the message was handled.</summary>
    public SideModifiers Modifiers { get; }

    /// <summary>True for <c>WM_CHAR</c>, the message that inserts text in an ordinary text box.</summary>
    public bool IsTyped => Message == CharMessage;
}
