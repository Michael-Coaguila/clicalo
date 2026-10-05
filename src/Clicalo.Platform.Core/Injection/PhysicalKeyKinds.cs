namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// Which physical keys open a menu when released alone (Alt, Win), whatever mode they were pressed in: their
/// release gets the menu mask first (blueprint §7.7). The Platform.Core twin of the Domain's <c>InjectedKeyKinds</c>.
/// </summary>
public static class PhysicalKeyKinds
{
    private const ushort VkMenu = 0x12;
    private const ushort VkLeftWin = 0x5B;
    private const ushort VkRightWin = 0x5C;
    private const ushort VkLeftMenu = 0xA4;
    private const ushort VkRightMenu = 0xA5;
    private const ushort ScanAlt = 0x38;
    private const ushort ScanLeftWin = 0x5B;
    private const ushort ScanRightWin = 0x5C;

    /// <summary>Whether releasing <paramref name="key"/> alone could open the Start menu or a menu bar.</summary>
    /// <param name="key">The key as it was pressed.</param>
    public static bool IsAltOrWin(PhysicalKey key) =>
        (key.Attributes & PhysicalKeyAttributes.ScanCodeMode) != PhysicalKeyAttributes.None
            ? key.Scan == ScanAlt
                || (
                    (key.Attributes & PhysicalKeyAttributes.Extended) != PhysicalKeyAttributes.None
                    && key.Scan is ScanLeftWin or ScanRightWin
                )
            : key.Vk is VkMenu or VkLeftMenu or VkRightMenu or VkLeftWin or VkRightWin;
}
