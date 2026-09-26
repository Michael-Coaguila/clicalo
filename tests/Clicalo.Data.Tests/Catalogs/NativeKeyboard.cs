using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// The user32 keyboard layout functions the Win32 mapping test compares keys.win32.json with.
/// Test-only: the product resolves keys in Clicalo.Platform.Windows.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeKeyboard
{
    /// <summary>MapVirtualKeyEx: virtual key to scan code, with the 0xE0/0xE1 prefix in the high byte.</summary>
    public const uint MapVkToVscEx = 4;

    /// <summary>MapVirtualKeyEx: scan code (with prefix) to a left/right-distinguishing virtual key.</summary>
    public const uint MapVscToVkEx = 3;

    /// <summary>LoadKeyboardLayout: do not notify the shell.</summary>
    public const uint KlfNoTellShell = 0x00000080;

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint MapVirtualKeyExW(uint code, uint mapType, nint layout);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern short VkKeyScanExW(char character, nint layout);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int GetKeyboardLayoutList(int count, [Out] nint[]? layouts);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern nint LoadKeyboardLayoutW(string layoutId, uint flags);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnloadKeyboardLayout(nint layout);
}
