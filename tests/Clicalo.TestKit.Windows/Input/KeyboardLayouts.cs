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
    private const ushort PrintScreenScanCode = 0x37;
    private const ushort NumLockScanCode = 0x45;

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

    /// <summary>The keyboard layout (<c>HKL</c>) of the calling thread.</summary>
    public static nint OfCurrentThread => (nint)PInvoke.GetKeyboardLayout(0);

    /// <summary>
    /// The scan code (without prefix) and extended flag of a virtual key in <paramref name="layout"/>, as a physical
    /// keystroke of that key reports them in <c>lParam</c>; scan code zero when there is none.
    /// </summary>
    /// <remarks>
    /// Based on <c>MapVirtualKeyEx</c> (<c>MAPVK_VK_TO_VSC_EX</c>), corrected where it disagrees with the keyboard:
    /// <list type="bullet">
    /// <item>
    /// the navigation keys (arrows, Insert, Delete, Home, End, Page Up and Page Down) come back as the numeric-keypad
    /// codes without the E0 prefix; the dedicated keys are extended, and sending them without
    /// <c>KEYEVENTF_EXTENDEDKEY</c> makes applications and Raw Input see the keypad key instead;
    /// </item>
    /// <item>Print Screen comes back as SysRq (<c>0x54</c>) instead of <c>E0 37</c>;</item>
    /// <item>Num Lock (<c>0x45</c>) is reported with the extended flag set;</item>
    /// <item>
    /// Pause is the only E1-prefixed key: its sequence cannot be expressed with <c>KEYEVENTF_EXTENDEDKEY</c>, so no
    /// informative scan code is given (zero).
    /// </item>
    /// </list>
    /// </remarks>
    public static (ushort ScanCode, bool Extended) ToScanCode(VirtualKeyCode key, nint layout)
    {
        switch ((VIRTUAL_KEY)key)
        {
            case VIRTUAL_KEY.VK_SNAPSHOT:
                return (PrintScreenScanCode, true);
            case VIRTUAL_KEY.VK_NUMLOCK:
                return (NumLockScanCode, true);
        }

        var mapped = PInvoke.MapVirtualKeyEx(
            (uint)key,
            MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX,
            (HKL)layout
        );
        return (mapped & 0xFF00) switch
        {
            0xE100 => (0, false),
            0xE000 => ((ushort)(mapped & 0xFF), true),
            _ => ((ushort)(mapped & 0xFF), IsNavigationKey(key)),
        };
    }

    private static bool IsNavigationKey(VirtualKeyCode key) =>
        (VIRTUAL_KEY)key
            is >= VIRTUAL_KEY.VK_PRIOR
                and <= VIRTUAL_KEY.VK_DOWN
                or VIRTUAL_KEY.VK_INSERT
                or VIRTUAL_KEY.VK_DELETE;
}
