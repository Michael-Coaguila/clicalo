using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Clicalo.TestKit;

/// <summary>
/// Splits a command line as Windows does (<c>CommandLineToArgvW</c>), so a test can check that what Clícalo writes for
/// a child process arrives as the same arguments. It starts nothing.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsCommandLine
{
    /// <summary>The arguments of <paramref name="commandLine"/>, the program name first.</summary>
    /// <param name="commandLine">A full command line, starting with the quoted program.</param>
    public static IReadOnlyList<string> Split(string commandLine)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandLine);
        var list = CommandLineToArgvW(commandLine, out var count);
        if (list == 0)
        {
            throw new InvalidOperationException(
                "CommandLineToArgvW failed: " + Marshal.GetLastPInvokeError()
            );
        }

        try
        {
            var arguments = new string[count];
            for (var i = 0; i < count; i++)
            {
                arguments[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(list, i * nint.Size))!;
            }

            return arguments;
        }
        finally
        {
            _ = LocalFree(list);
        }
    }

    [DllImport("shell32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CommandLineToArgvW(string commandLine, out int count);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint LocalFree(nint memory);
}
