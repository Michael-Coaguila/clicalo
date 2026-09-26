namespace Clicalo.Tools.InputProbe;

/// <summary>
/// The keystroke fields packed in the <c>lParam</c> of <c>WM_KEYDOWN</c>, <c>WM_KEYUP</c>, <c>WM_SYSKEY*</c> and
/// the character messages (see "Keystroke message flags" in the Win32 documentation).
/// </summary>
internal readonly record struct KeystrokeFlags(
    ushort RepeatCount,
    byte ScanCode,
    bool IsExtended,
    bool IsAltDown,
    bool WasDown,
    bool IsReleased
)
{
    /// <summary>Decodes the low 32 bits of <paramref name="lParam"/>.</summary>
    public static KeystrokeFlags FromLParam(nint lParam)
    {
        var bits = unchecked((uint)lParam);
        return new KeystrokeFlags(
            RepeatCount: (ushort)(bits & 0xFFFF),
            ScanCode: (byte)((bits >> 16) & 0xFF),
            IsExtended: (bits & (1u << 24)) != 0,
            IsAltDown: (bits & (1u << 29)) != 0,
            WasDown: (bits & (1u << 30)) != 0,
            IsReleased: (bits & (1u << 31)) != 0
        );
    }
}
