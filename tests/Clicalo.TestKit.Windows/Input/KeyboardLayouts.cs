using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// Keyboard-layout translations, so that expectations follow the layout of the window under test ("the virtual key
/// the application sees is the one its own layout translates", blueprint §7.7).
/// </summary>
public static class KeyboardLayouts
{
    /// <summary>The keyboard layout (<c>HKL</c>) of the thread that owns <paramref name="window"/>.</summary>
    public static nint OfWindow(nint window) =>
        (nint)PInvoke.GetKeyboardLayout(PInvoke.GetWindowThreadProcessId((HWND)window));

    /// <summary>
    /// The side-specific virtual key that <paramref name="layout"/> assigns to a scan code
    /// (<c>MapVirtualKeyEx</c>, <c>MAPVK_VSC_TO_VK_EX</c>); <see cref="VirtualKeyCode.None"/> when it has none.
    /// </summary>
    public static VirtualKeyCode ToVirtualKey(ushort scanCode, bool extended, nint layout)
    {
        var code = extended ? 0xE000u | scanCode : scanCode;
        return (VirtualKeyCode)
            PInvoke.MapVirtualKeyEx(code, MAP_VIRTUAL_KEY_TYPE.MAPVK_VSC_TO_VK_EX, (HKL)layout);
    }

    /// <summary>
    /// The scan code (without prefix) and extended flag that <paramref name="layout"/> assigns to a virtual key
    /// (<c>MAPVK_VK_TO_VSC_EX</c>); scan code zero when it has none.
    /// </summary>
    public static (ushort ScanCode, bool Extended) ToScanCode(VirtualKeyCode key, nint layout)
    {
        var mapped = PInvoke.MapVirtualKeyEx(
            (uint)key,
            MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX,
            (HKL)layout
        );
        return ((ushort)(mapped & 0xFF), (mapped & 0xFF00) is 0xE000 or 0xE100);
    }
}
