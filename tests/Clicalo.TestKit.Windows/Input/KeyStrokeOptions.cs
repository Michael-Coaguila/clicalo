namespace Clicalo.TestKit.Windows.Input;

/// <summary>Keyboard event flags, with the values of Win32 <c>KEYEVENTF_*</c>.</summary>
[Flags]
public enum KeyStrokeOptions : uint
{
    None = 0,

    /// <summary><c>KEYEVENTF_EXTENDEDKEY</c>: the E0-prefixed key (right Ctrl and Alt, arrows, navigation keys...).</summary>
    ExtendedKey = 0x0001,

    /// <summary><c>KEYEVENTF_KEYUP</c>: a release.</summary>
    KeyUp = 0x0002,

    /// <summary><c>KEYEVENTF_UNICODE</c>: the scan code field carries a UTF-16 code unit (VK_PACKET).</summary>
    Unicode = 0x0004,

    /// <summary><c>KEYEVENTF_SCANCODE</c>: the virtual key is ignored and the scan code identifies the key.</summary>
    ScanCode = 0x0008,
}
