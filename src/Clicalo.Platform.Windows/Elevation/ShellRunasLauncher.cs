using System.Runtime.InteropServices;
using Clicalo.Application.Ports;

namespace Clicalo.Platform.Windows.Elevation;

/// <summary>
/// <c>ShellExecuteExW</c> with the verb <c>runas</c> (blueprint §3.3 rule 2, ADR-0027): Windows shows its UAC prompt and,
/// when the person accepts, starts the executable as administrator. <c>ERROR_CANCELLED</c> (1223) is the person saying
/// no, or no administrator credentials. Called on a short-lived STA thread, never on the UI thread.
/// </summary>
internal sealed unsafe partial class ShellRunasLauncher : IElevationLauncher
{
    private const uint SeeMaskNoAsync = 0x00000100;
    private const uint SeeMaskFlagNoUi = 0x00000400;
    private const int ShowNormal = 1;
    private const int ErrorCancelled = 1223;

    /// <inheritdoc />
    public ElevationOutcome Launch(string executable, string arguments)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        var directory = Path.GetDirectoryName(executable) ?? string.Empty;
        fixed (char* verb = "runas")
        fixed (char* file = executable)
        fixed (char* parameters = arguments)
        fixed (char* folder = directory)
        {
            var info = new ShellExecuteInfo
            {
                Size = (uint)sizeof(ShellExecuteInfo),
                Mask = SeeMaskNoAsync | SeeMaskFlagNoUi,
                Verb = verb,
                File = file,
                Parameters = parameters,
                Directory = folder,
                Show = ShowNormal,
            };
            if (ShellExecuteExW(&info) != 0)
            {
                return ElevationOutcome.Started;
            }
        }

        return Marshal.GetLastPInvokeError() == ErrorCancelled
            ? ElevationOutcome.Cancelled
            : ElevationOutcome.Failed;
    }

    [LibraryImport("shell32.dll", EntryPoint = "ShellExecuteExW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int ShellExecuteExW(ShellExecuteInfo* info);

    /// <summary><c>SHELLEXECUTEINFOW</c>, with only the fields <c>runas</c> needs set.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ShellExecuteInfo
    {
        public uint Size;
        public uint Mask;
        public nint Window;
        public char* Verb;
        public char* File;
        public char* Parameters;
        public char* Directory;
        public int Show;
        public nint InstanceApp;
        public nint IdList;
        public char* Class;
        public nint ClassKey;
        public uint HotKey;
        public nint IconOrMonitor;
        public nint Process;
    }
}
