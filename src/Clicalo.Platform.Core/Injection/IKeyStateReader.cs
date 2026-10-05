namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// What Windows says is down right now (ADR-0023). <see cref="SystemKeyState"/> is the real one; the tests give their
/// own, so <see cref="PressedInputRelease"/> and Sentinel are tested without a keyboard.
/// </summary>
public interface IKeyStateReader
{
    /// <summary>
    /// Whether the key state can be read and input sent: the input desktop is this process's. While the secure
    /// desktop has the input (a locked session, UAC, Ctrl+Alt+Supr) Windows reports every key up and refuses
    /// <c>SendInput</c>.
    /// </summary>
    bool CanRead { get; }

    /// <summary>Whether <paramref name="virtualKey"/> is down (the high bit of <c>GetAsyncKeyState</c>).</summary>
    /// <param name="virtualKey">A virtual key, 0x01 to 0xFE.</param>
    bool IsDown(byte virtualKey);

    /// <summary>
    /// The scan code of <paramref name="virtualKey"/> (<c>MapVirtualKey</c> with <c>MAPVK_VK_TO_VSC_EX</c>): the high
    /// byte is <c>0xE0</c> or <c>0xE1</c> for an extended key, 0 otherwise.
    /// </summary>
    /// <param name="virtualKey">A virtual key.</param>
    ushort ScanCode(byte virtualKey);
}
