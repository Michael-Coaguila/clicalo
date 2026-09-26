using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Injection;

/// <summary>
/// Scan codes and extended flags of virtual keys (blueprint §7.7), including the keys where
/// <c>MapVirtualKeyEx</c> disagrees with the physical keyboard. Every key below has the same scan code in all
/// layouts, so the expectations hold on any machine.
/// </summary>
public sealed class KeyboardLayoutsTests
{
    [Theory]
    [Trait("Req", "NFR-004")]
    // MapVirtualKeyEx answers the numeric-keypad codes without E0 for the navigation keys.
    [InlineData(0x25, 0x4B, true)] // VK_LEFT
    [InlineData(0x26, 0x48, true)] // VK_UP
    [InlineData(0x27, 0x4D, true)] // VK_RIGHT
    [InlineData(0x28, 0x50, true)] // VK_DOWN
    [InlineData(0x21, 0x49, true)] // VK_PRIOR
    [InlineData(0x22, 0x51, true)] // VK_NEXT
    [InlineData(0x23, 0x4F, true)] // VK_END
    [InlineData(0x24, 0x47, true)] // VK_HOME
    [InlineData(0x2D, 0x52, true)] // VK_INSERT
    [InlineData(0x2E, 0x53, true)] // VK_DELETE
    // ...and SysRq for Print Screen, Num Lock without its extended flag, Pause with the E1 prefix.
    [InlineData(0x2C, 0x37, true)] // VK_SNAPSHOT
    [InlineData(0x90, 0x45, true)] // VK_NUMLOCK
    [InlineData(0x13, 0x00, false)] // VK_PAUSE: no informative scan code
    // Keys MapVirtualKeyEx already answers correctly.
    [InlineData(0x64, 0x4B, false)] // VK_NUMPAD4
    [InlineData(0xA2, 0x1D, false)] // VK_LCONTROL
    [InlineData(0xA3, 0x1D, true)] // VK_RCONTROL
    [InlineData(0xA4, 0x38, false)] // VK_LMENU
    [InlineData(0xA5, 0x38, true)] // VK_RMENU
    [InlineData(0x6F, 0x35, true)] // VK_DIVIDE
    [InlineData(0x0D, 0x1C, false)] // VK_RETURN (main Enter)
    [InlineData(0x5B, 0x5B, true)] // VK_LWIN
    public void A_virtual_key_resolves_to_the_scan_code_and_extended_flag_of_the_physical_key(
        ushort virtualKey,
        ushort scanCode,
        bool extended
    ) =>
        KeyboardLayouts
            .ToScanCode((VirtualKeyCode)virtualKey, KeyboardLayouts.OfCurrentThread)
            .ShouldBe((scanCode, extended));

    [Theory]
    [InlineData(0x4B, true, VirtualKeyCode.Left)]
    [InlineData(0x1D, false, VirtualKeyCode.LeftControl)]
    [InlineData(0x1D, true, VirtualKeyCode.RightControl)]
    [InlineData(0x38, false, VirtualKeyCode.LeftMenu)]
    [InlineData(0x38, true, VirtualKeyCode.RightMenu)]
    public void A_scan_code_and_its_prefix_resolve_to_the_side_specific_virtual_key(
        ushort scanCode,
        bool extended,
        VirtualKeyCode expected
    ) =>
        KeyboardLayouts
            .ToVirtualKey(scanCode, extended, KeyboardLayouts.OfCurrentThread)
            .ShouldBe(expected);
}
