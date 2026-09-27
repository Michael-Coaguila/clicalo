using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Clicalo.App.Interop;

/// <summary>
/// The few Win32 calls the composition root needs and no adapter offers yet: the two-way check of the single-instance
/// pipe (blueprint §3.4, ADR-0010), the image name of a process and the keyboard layout of the foreground thread
/// (§7.7, §7.9). No strings are marshalled: the image name comes back in a caller buffer.
/// </summary>
/// <remarks>
/// Clicalo.App has no CsWin32 (its project file is frozen in M2): these move to Platform.Windows/SingleInstance and to
/// the engine's layout builder when the integration step adds them there (docs/testing/spikes/M2-ownership.md).
/// </remarks>
internal static class NativeMethods
{
    /// <summary><c>PROCESS_QUERY_LIMITED_INFORMATION</c>: works across integrity levels.</summary>
    public const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe, out uint sessionId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId
    );

    [DllImport(
        "kernel32.dll",
        SetLastError = true,
        CharSet = CharSet.Unicode,
        EntryPoint = "QueryFullProcessImageNameW"
    )]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryFullProcessImageName(
        SafeProcessHandle process,
        uint flags,
        [Out] char[] buffer,
        ref uint size
    );

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern nint GetKeyboardLayout(uint threadId);
}
