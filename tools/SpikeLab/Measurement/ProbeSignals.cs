using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>Classifies the messages InputProbe reports into the counters of S4 («F24 = N, caracteres = N, menú = N»).</summary>
internal static class ProbeSignals
{
    private const uint KeyDown = 0x0100;
    private const uint KeyUp = 0x0101;
    private const uint Char = 0x0102;
    private const uint DeadChar = 0x0103;
    private const uint SysKeyDown = 0x0104;
    private const uint SysKeyUp = 0x0105;
    private const uint SysChar = 0x0106;
    private const uint SysDeadChar = 0x0107;
    private const uint UniChar = 0x0109;
    private const uint SysCommand = 0x0112;
    private const ulong KeyMenu = 0xF100;
    private const ushort F24 = 0x87;

    /// <summary>Classifies one event of the probe.</summary>
    public static ProbeSignal Classify(ProbeEvent probeEvent) =>
        probeEvent switch
        {
            KeyMessageEvent key => Classify(key.Message, key.WParam, (ushort)key.VirtualKey),
            ProbeMessageEvent message => Classify(message.Message, message.WParam, 0),
            _ => ProbeSignal.None,
        };

    /// <summary>Classifies a window message by number, <c>wParam</c> and virtual key.</summary>
    public static ProbeSignal Classify(uint message, ulong wParam, ushort virtualKey)
    {
        switch (message)
        {
            case KeyDown or KeyUp or SysKeyDown or SysKeyUp:
                if (virtualKey == F24)
                {
                    return message is KeyDown or SysKeyDown ? ProbeSignal.F24 : ProbeSignal.None;
                }

                return IsModifier(virtualKey) ? ProbeSignal.ModifierKey : ProbeSignal.None;
            case Char or DeadChar or SysChar or SysDeadChar or UniChar:
                return ProbeSignal.Character;
            case SysCommand when (wParam & 0xFFF0) == KeyMenu:
                return ProbeSignal.Menu;
            default:
                return ProbeSignal.None;
        }
    }

    private static bool IsModifier(ushort virtualKey) =>
        virtualKey is >= 0x10 and <= 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;
}
