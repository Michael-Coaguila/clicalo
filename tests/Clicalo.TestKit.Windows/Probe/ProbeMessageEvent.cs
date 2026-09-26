using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>An event produced by a window message received by the probe.</summary>
public abstract class ProbeMessageEvent : ProbeEvent
{
    private protected ProbeMessageEvent(JsonElement json, string line)
        : base(json, line)
    {
        Message = JsonFields.UInt32(json, ProbeFields.Message);
        MessageName = JsonFields.String(json, ProbeFields.MessageName);
        MessageTime = JsonFields.Int32(json, ProbeFields.MessageTime);
        WParam = JsonFields.UInt64(json, ProbeFields.WParam);
        LParam = JsonFields.Int64(json, ProbeFields.LParam);
        ExtraInfo = JsonFields.Int64(json, ProbeFields.ExtraInfo);
    }

    /// <summary>Window message number.</summary>
    public uint Message { get; }

    /// <summary>Symbolic name, for example <c>WM_KEYDOWN</c>.</summary>
    public string MessageName { get; }

    /// <summary><c>GetMessageTime</c> (milliseconds since boot, wraps around).</summary>
    public int MessageTime { get; }

    /// <summary>Raw <c>wParam</c>.</summary>
    public ulong WParam { get; }

    /// <summary>Raw <c>lParam</c>.</summary>
    public long LParam { get; }

    /// <summary>
    /// The <c>dwExtraInfo</c> the input was injected with (<c>RAWKEYBOARD.ExtraInformation</c> for Raw Input).
    /// Input from <see cref="Input.TestKeyboardInjector"/> carries <see cref="Input.TestKeyboardInjector.ExtraInfoMarker"/>.
    /// </summary>
    public long ExtraInfo { get; }
}
