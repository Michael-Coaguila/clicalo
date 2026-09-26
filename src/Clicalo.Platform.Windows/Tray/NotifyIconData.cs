using System.Runtime.InteropServices;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// <c>NOTIFYICONDATAW</c> with the 64-bit layout (natural alignment, 976 bytes). CsWin32 cannot generate it for AnyCPU
/// because 32-bit Windows packs it to 1 byte; Clícalo ships x64 and ARM64 only, and <see cref="ShellNotifyIcon"/>
/// refuses to run in a 32-bit process.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NotifyIconData
{
    public uint Size;
    public nint Window;
    public uint Id;
    public uint Flags;
    public uint CallbackMessage;
    public nint Icon;
    public fixed char Tip[128];
    public uint State;
    public uint StateMask;
    public fixed char Info[256];
    public uint TimeoutOrVersion;
    public fixed char InfoTitle[64];
    public uint InfoFlags;
    public Guid Item;
    public nint BalloonIcon;
}
