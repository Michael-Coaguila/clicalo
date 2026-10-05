using Clicalo.Domain.Keys;

namespace Clicalo.Domain.KeySafety;

/// <summary>
/// What a physical key is, whatever the mode it was sent in: the ledger masks the release of Alt and Win (blueprint
/// §7.7) and counts Shift presses (SEG-008). Virtual keys and set-1 scan codes of the Win32 keyboard.
/// </summary>
public static class InjectedKeyKinds
{
    private const ushort VkShift = 0x10;
    private const ushort VkMenu = 0x12;
    private const ushort VkLeftWin = 0x5B;
    private const ushort VkRightWin = 0x5C;
    private const ushort VkLeftShift = 0xA0;
    private const ushort VkRightShift = 0xA1;
    private const ushort VkLeftMenu = 0xA4;
    private const ushort VkRightMenu = 0xA5;
    private const ushort ScanLeftShift = 0x2A;
    private const ushort ScanRightShift = 0x36;
    private const ushort ScanAlt = 0x38;
    private const ushort ScanLeftWin = 0x5B;
    private const ushort ScanRightWin = 0x5C;

    /// <summary>
    /// Whether releasing <paramref name="key"/> alone could open the Start menu or a menu bar (Alt or Win, either
    /// side), so a safety release sends the menu mask first.
    /// </summary>
    /// <param name="key">The key as it was pressed.</param>
    public static bool IsAltOrWin(InjectedKey key) =>
        key.Mode == InjectionMode.ScanCode
            ? key.Scan == ScanAlt || (key.Extended && key.Scan is ScanLeftWin or ScanRightWin)
            : key.Vk is VkMenu or VkLeftMenu or VkRightMenu or VkLeftWin or VkRightWin;

    /// <summary>Whether <paramref name="key"/> is a Shift key (SEG-008 counts its presses).</summary>
    /// <param name="key">The key as it is pressed.</param>
    public static bool IsShift(InjectedKey key) =>
        key.Mode == InjectionMode.ScanCode
            ? key.Scan is ScanLeftShift or ScanRightShift
            : key.Vk is VkShift or VkLeftShift or VkRightShift;
}
