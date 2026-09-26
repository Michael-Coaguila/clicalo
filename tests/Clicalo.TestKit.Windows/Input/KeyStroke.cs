using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// One keyboard event of a test batch, in one of the three injection modes of blueprint §7.7:
/// virtual key (normal mode), scan code (compatible mode) or Unicode (Text actions).
/// </summary>
/// <param name="VirtualKey">Virtual key (virtual-key mode only; zero otherwise).</param>
/// <param name="ScanCode">
/// Scan code without the E0 prefix (scan-code mode), the UTF-16 unit (Unicode mode) or, in virtual-key mode, the
/// informative scan code; zero there means "resolve it with the target's keyboard layout when sending".
/// </param>
/// <param name="Flags">Win32 <c>KEYEVENTF_*</c> flags.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct KeyStroke(
    VirtualKeyCode VirtualKey,
    ushort ScanCode,
    KeyStrokeOptions Flags
)
{
    /// <summary>True for a release.</summary>
    public bool IsKeyUp => Flags.HasFlag(KeyStrokeOptions.KeyUp);

    /// <summary>True for an E0-prefixed (extended) key.</summary>
    public bool IsExtended => Flags.HasFlag(KeyStrokeOptions.ExtendedKey);

    /// <summary>True for scan-code mode (<c>wVk = 0</c>, <c>KEYEVENTF_SCANCODE</c>).</summary>
    public bool IsScanCodeMode => Flags.HasFlag(KeyStrokeOptions.ScanCode);

    /// <summary>True for Unicode mode (<c>KEYEVENTF_UNICODE</c>).</summary>
    public bool IsUnicode => Flags.HasFlag(KeyStrokeOptions.Unicode);

    /// <summary>
    /// Identifies the physical or logical key, so that a release can be matched with its press: the virtual key,
    /// the scan code plus extended flag, or the UTF-16 unit.
    /// </summary>
    internal (int Mode, int Code) KeyIdentity =>
        IsUnicode ? (2, ScanCode)
        : IsScanCodeMode ? (1, ScanCode | (IsExtended ? 0xE000 : 0))
        : (0, (int)VirtualKey);

    /// <summary>Virtual-key press; the informative scan code and extended flag come from the target's layout.</summary>
    public static KeyStroke Press(VirtualKeyCode key) => new(key, 0, KeyStrokeOptions.None);

    /// <summary>Virtual-key release; the informative scan code and extended flag come from the target's layout.</summary>
    public static KeyStroke Release(VirtualKeyCode key) => new(key, 0, KeyStrokeOptions.KeyUp);

    /// <summary>Scan-code press (<c>wVk = 0</c>), as compatible mode sends it.</summary>
    public static KeyStroke PressScanCode(ushort scanCode, bool extended) =>
        new(VirtualKeyCode.None, ValidScanCode(scanCode), ScanCodeFlags(extended));

    /// <summary>Scan-code release (<c>wVk = 0</c>), as compatible mode sends it.</summary>
    public static KeyStroke ReleaseScanCode(ushort scanCode, bool extended) =>
        new(
            VirtualKeyCode.None,
            ValidScanCode(scanCode),
            ScanCodeFlags(extended) | KeyStrokeOptions.KeyUp
        );

    /// <summary>Unicode press of one UTF-16 code unit.</summary>
    public static KeyStroke PressUnicode(char unit) =>
        new(VirtualKeyCode.None, unit, KeyStrokeOptions.Unicode);

    /// <summary>Unicode release of one UTF-16 code unit.</summary>
    public static KeyStroke ReleaseUnicode(char unit) =>
        new(VirtualKeyCode.None, unit, KeyStrokeOptions.Unicode | KeyStrokeOptions.KeyUp);

    public override string ToString()
    {
        var direction = IsKeyUp ? "up" : "down";
        return IsUnicode
                ? string.Create(CultureInfo.InvariantCulture, $"U+{ScanCode:X4} {direction}")
            : IsScanCodeMode
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"SC {(IsExtended ? "E0 " : string.Empty)}0x{ScanCode:X2} {direction}"
                )
            : string.Create(CultureInfo.InvariantCulture, $"VK {VirtualKey} {direction}");
    }

    private static ushort ValidScanCode(ushort scanCode) =>
        scanCode is > 0 and <= 0xFF
            ? scanCode
            : throw new ArgumentOutOfRangeException(
                nameof(scanCode),
                scanCode,
                "Pass the scan code without prefix (1–255)."
            );

    private static KeyStrokeOptions ScanCodeFlags(bool extended) =>
        KeyStrokeOptions.ScanCode
        | (extended ? KeyStrokeOptions.ExtendedKey : KeyStrokeOptions.None);
}
