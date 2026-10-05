using Clicalo.Platform.Core.Injection;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// A keyboard and mouse state without Windows: the keys down, whether the input desktop is readable, and the scan code
/// of each virtual key (extended keys with the <c>E0</c> prefix). <see cref="FakeSender"/> lifts what it accepts.
/// </summary>
internal sealed class FakeKeyState : IKeyStateReader
{
    private static readonly HashSet<byte> Extended =
    [
        0x5B,
        0x5C,
        0xA3,
        0xA5,
        0x25,
        0x26,
        0x27,
        0x28,
        0x2E,
    ];

    public HashSet<byte> Down { get; } = [];

    public bool CanRead { get; set; } = true;

    public bool IsDown(byte virtualKey) => CanRead && Down.Contains(virtualKey);

    public ushort ScanCode(byte virtualKey) =>
        (ushort)((Extended.Contains(virtualKey) ? 0xE000 : 0) | (virtualKey ^ 0x40));
}
