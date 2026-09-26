using System.Runtime.InteropServices;
using Windows.Win32.UI.Shell;

namespace Clicalo.Platform.Windows.Tray;

/// <summary><c>Shell_NotifyIconW</c> over <see cref="NotifyIconData"/> (see there why it is not generated).</summary>
internal static unsafe partial class ShellNotifyIcon
{
    /// <summary>Size of <see cref="NotifyIconData"/> on 64-bit Windows, as <c>sizeof(NOTIFYICONDATAW)</c>.</summary>
    public const int ExpectedSize = 976;

    /// <summary>Sends <paramref name="message"/> for <paramref name="data"/>; false when the shell refused it.</summary>
    public static bool Send(NOTIFY_ICON_MESSAGE message, ref NotifyIconData data)
    {
        if (!Environment.Is64BitProcess || sizeof(NotifyIconData) != ExpectedSize)
        {
            throw new PlatformNotSupportedException(
                "The notification area icon needs a 64-bit process (x64 or ARM64)."
            );
        }

        data.Size = (uint)sizeof(NotifyIconData);
        fixed (NotifyIconData* pointer = &data)
        {
            return ShellNotifyIconW((uint)message, pointer) != 0;
        }
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int ShellNotifyIconW(uint message, NotifyIconData* data);
}
