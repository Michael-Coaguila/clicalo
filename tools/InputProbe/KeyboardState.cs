using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Tools.InputProbe;

/// <summary>Reads the keyboard state of the window thread, as an application would see it.</summary>
internal static class KeyboardState
{
    private const ushort ShiftKey = (ushort)VIRTUAL_KEY.VK_SHIFT;
    private const ushort ControlKey = (ushort)VIRTUAL_KEY.VK_CONTROL;
    private const ushort MenuKey = (ushort)VIRTUAL_KEY.VK_MENU;

    // Bit order shared with SideModifiers in Clicalo.TestKit.Windows.
    private static readonly VIRTUAL_KEY[] SideModifierKeys =
    [
        VIRTUAL_KEY.VK_LSHIFT,
        VIRTUAL_KEY.VK_RSHIFT,
        VIRTUAL_KEY.VK_LCONTROL,
        VIRTUAL_KEY.VK_RCONTROL,
        VIRTUAL_KEY.VK_LMENU,
        VIRTUAL_KEY.VK_RMENU,
        VIRTUAL_KEY.VK_LWIN,
        VIRTUAL_KEY.VK_RWIN,
    ];

    /// <summary>
    /// Side-specific modifiers down according to <c>GetKeyState</c>, which is synchronized with the message being
    /// handled: bit 0 left Shift, 1 right Shift, 2 left Ctrl, 3 right Ctrl, 4 left Alt, 5 right Alt, 6 left Win,
    /// 7 right Win.
    /// </summary>
    public static int SideModifiers()
    {
        var bits = 0;
        for (var i = 0; i < SideModifierKeys.Length; i++)
        {
            if ((PInvoke.GetKeyState((int)SideModifierKeys[i]) & 0x8000) != 0)
            {
                bits |= 1 << i;
            }
        }

        return bits;
    }

    /// <summary>
    /// Turns the generic Shift, Ctrl and Alt codes of a key message into the side-specific ones (for example
    /// <c>VK_RCONTROL</c>) using the scan code, the extended flag and the thread's keyboard layout. Other keys, and
    /// modifiers sent without a scan code, are returned unchanged.
    /// </summary>
    public static ushort SideSpecific(ushort virtualKey, KeystrokeFlags flags)
    {
        if (virtualKey is not (ShiftKey or ControlKey or MenuKey) || flags.ScanCode == 0)
        {
            return virtualKey;
        }

        var scanCode = flags.IsExtended ? 0xE000u | flags.ScanCode : flags.ScanCode;
        var mapped = PInvoke.MapVirtualKeyEx(
            scanCode,
            MAP_VIRTUAL_KEY_TYPE.MAPVK_VSC_TO_VK_EX,
            PInvoke.GetKeyboardLayout(0)
        );
        return mapped == 0 ? virtualKey : (ushort)mapped;
    }
}
