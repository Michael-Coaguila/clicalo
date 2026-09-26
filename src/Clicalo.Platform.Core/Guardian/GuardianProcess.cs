using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// A process of the guardian pair (blueprint §3.1): the main process starts Sentinel with an explicit
/// <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c>, so Sentinel inherits exactly the handles of <see cref="SentinelStartInfo"/>
/// and nothing else; Sentinel starts the main process again with no handle at all. Never through a command interpreter:
/// the executable is a full path and the arguments are simple tokens.
/// </summary>
public sealed unsafe class GuardianProcess : IDisposable
{
    private const uint ExtendedStartupInfoPresent = 0x0008_0000;
    private const uint Infinite = 0xFFFF_FFFF;
    private const uint StillActive = 259;

    private HANDLE _process;

    private GuardianProcess(HANDLE process, int id)
    {
        _process = process;
        Id = id;
    }

    /// <summary>The process id.</summary>
    public int Id { get; }

    /// <summary>The process handle (valid until <see cref="Dispose"/>).</summary>
    public nint Handle => (nint)_process.Value;

    /// <summary>Whether the process has ended.</summary>
    public bool HasExited => GuardianHandles.WaitForExit((nint)_process.Value, TimeSpan.Zero);

    /// <summary>The exit code, or <see langword="null"/> while it runs.</summary>
    public int? ExitCode
    {
        get
        {
            uint code;
            return PInvoke.GetExitCodeProcess(_process, &code) && code != StillActive
                ? (int)code
                : null;
        }
    }

    /// <summary>
    /// Starts <paramref name="executable"/> inheriting exactly <paramref name="inheritedHandles"/> (each must be
    /// inheritable). With no handle, nothing is inherited.
    /// </summary>
    /// <param name="executable">Full path of the executable.</param>
    /// <param name="arguments">Arguments without spaces or quotes.</param>
    /// <param name="inheritedHandles">The handles the child inherits.</param>
    public static GuardianProcess Start(
        string executable,
        IReadOnlyList<string> arguments,
        ReadOnlySpan<nint> inheritedHandles
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Path.IsPathFullyQualified(executable))
        {
            throw new ArgumentException("The executable must be a full path.", nameof(executable));
        }

        var commandLine = new StringBuilder().Append('"').Append(executable).Append('"');
        foreach (var argument in arguments)
        {
            if (argument.AsSpan().IndexOfAny(" \t\"") >= 0)
            {
                throw new ArgumentException(
                    "Arguments are simple tokens: " + argument,
                    nameof(arguments)
                );
            }

            commandLine.Append(' ').Append(argument);
        }

        var command = (commandLine.ToString() + '\0').ToCharArray();
        var startup = new STARTUPINFOEXW();
        startup.StartupInfo.cb = (uint)sizeof(STARTUPINFOEXW);
        nuint listSize = 0;
        byte[]? list = null;
        var handles = inheritedHandles.ToArray();
        var flags = (PROCESS_CREATION_FLAGS)0;
        fixed (nint* handleList = handles)
        fixed (char* commandText = command)
        fixed (char* application = executable)
        {
            try
            {
                if (handles.Length > 0)
                {
                    PInvoke.InitializeProcThreadAttributeList(default, 1, 0, &listSize);
                    list = new byte[(int)listSize];
                    fixed (byte* listMemory = list)
                    {
                        startup.lpAttributeList = new LPPROC_THREAD_ATTRIBUTE_LIST(listMemory);
                        if (
                            !PInvoke.InitializeProcThreadAttributeList(
                                startup.lpAttributeList,
                                1,
                                0,
                                &listSize
                            )
                        )
                        {
                            throw new Win32Exception(Marshal.GetLastPInvokeError());
                        }

                        if (
                            !PInvoke.UpdateProcThreadAttribute(
                                startup.lpAttributeList,
                                0,
                                PInvoke.PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                                handleList,
                                (nuint)(handles.Length * sizeof(nint)),
                                null,
                                null
                            )
                        )
                        {
                            throw new Win32Exception(Marshal.GetLastPInvokeError());
                        }

                        flags = (PROCESS_CREATION_FLAGS)ExtendedStartupInfoPresent;
                        return Create(
                            application,
                            commandText,
                            handles.Length > 0,
                            flags,
                            &startup
                        );
                    }
                }

                return Create(application, commandText, inherit: false, flags, &startup);
            }
            finally
            {
                if (!startup.lpAttributeList.IsNull)
                {
                    PInvoke.DeleteProcThreadAttributeList(startup.lpAttributeList);
                }
            }
        }
    }

    /// <summary>Waits for the process to end.</summary>
    /// <param name="timeout">How long.</param>
    public bool WaitForExit(TimeSpan timeout) =>
        GuardianHandles.WaitForExit((nint)_process.Value, timeout);

    /// <summary>Ends the process at once (the chaos tests of S9: «muerte del proceso»).</summary>
    /// <param name="exitCode">Its exit code.</param>
    public bool Kill(int exitCode) => PInvoke.TerminateProcess(_process, (uint)exitCode);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_process.IsNull)
        {
            PInvoke.CloseHandle(_process);
            _process = default;
        }
    }

    private static GuardianProcess Create(
        char* application,
        char* commandLine,
        bool inherit,
        PROCESS_CREATION_FLAGS flags,
        STARTUPINFOEXW* startup
    )
    {
        PROCESS_INFORMATION information;
        if (
            !PInvoke.CreateProcess(
                new PCWSTR(application),
                new PWSTR(commandLine),
                null,
                null,
                inherit,
                flags,
                null,
                default,
                (STARTUPINFOW*)startup,
                &information
            )
        )
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        PInvoke.CloseHandle(information.hThread);
        return new GuardianProcess(information.hProcess, (int)information.dwProcessId);
    }
}
