namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// Side-specific modifiers held down, as the probe reads them with <c>GetKeyState</c> while handling a message.
/// The bit order is the probe's (<c>KeyboardState.SideModifiers</c> in tools/InputProbe).
/// </summary>
[Flags]
public enum SideModifiers
{
    None = 0,
    LeftShift = 1 << 0,
    RightShift = 1 << 1,
    LeftControl = 1 << 2,
    RightControl = 1 << 3,
    LeftAlt = 1 << 4,
    RightAlt = 1 << 5,
    LeftWindows = 1 << 6,
    RightWindows = 1 << 7,
}
