using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Clicalo.Performance;

/// <summary>
/// The instance of Clícalo a measurement starts, seen from outside the process (App/SingleInstance/InstanceIdentity,
/// blueprint §3.4, SIS-003): the names of its mutex and of its pipe, and whether one is running. Every measurement
/// starts <c>Clicalo.exe</c> as a process with a data folder of its own (<c>--data</c>), and a start with
/// <c>--data</c> is an instance of its own (ADR-0030): it never finds the Clícalo the person uses, and a second start
/// with the same folder finds the first.
/// </summary>
internal static class RunningInstance
{
    /// <summary>The longest wait for the previous instance of a measurement to be gone after it was ended.</summary>
    public static readonly TimeSpan GoneTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The mutex of the instance that runs with <c>--data <paramref name="dataDirectory"/></c>:
    /// <c>Local\Clicalo.{sidHash}.d{16 hex}.Instance</c> (ADR-0030).
    /// </summary>
    /// <param name="dataDirectory">The folder of <c>--data</c>.</param>
    public static string MutexName(string dataDirectory) =>
        @"Local\Clicalo." + SidHash() + ScopeOf(dataDirectory) + ".Instance";

    /// <summary>The pipe of that instance, without the <c>\\.\pipe\</c> prefix.</summary>
    /// <param name="dataDirectory">The folder of <c>--data</c>.</param>
    public static string PipeName(string dataDirectory)
    {
        using var process = Process.GetCurrentProcess();
        return "Clicalo."
            + SidHash()
            + ScopeOf(dataDirectory)
            + "."
            + process.SessionId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a Clícalo with that data folder is running now.</summary>
    /// <param name="dataDirectory">The folder of <c>--data</c>.</param>
    public static bool Exists(string dataDirectory)
    {
        if (!Mutex.TryOpenExisting(MutexName(dataDirectory), out var mutex))
        {
            return false;
        }

        mutex.Dispose();
        return true;
    }

    /// <summary>
    /// Waits until no Clícalo with that data folder is running; the process a measurement just ended may need a moment
    /// to close its handles.
    /// </summary>
    /// <param name="dataDirectory">The folder of <c>--data</c>.</param>
    /// <param name="limit">The longest wait.</param>
    /// <exception cref="InvalidOperationException">An instance is still running after <paramref name="limit"/>.</exception>
    public static void WaitUntilGone(string dataDirectory, TimeSpan limit)
    {
        if (!SpinWait.SpinUntil(() => !Exists(dataDirectory), limit))
        {
            throw new InvalidOperationException(
                "Another Clícalo with the data folder of this measurement is running: every start would only show "
                    + "that instance (exit code 0, 2 or 3). Close it and run «cl perf» again."
            );
        }
    }

    /// <summary>
    /// The suffix of an isolated data folder, as <c>InstanceIdentity.ScopeOf</c> derives it: <c>.d</c> and the first 16
    /// hexadecimal characters of SHA-256 over the UTF-8 of its full path, without a trailing separator, in upper case.
    /// </summary>
    /// <param name="dataDirectory">The folder of <c>--data</c>.</param>
    public static string ScopeOf(string dataDirectory)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant()));
        return ".d" + Convert.ToHexStringLower(hash)[..16];
    }

    /// <summary>
    /// The first 16 hexadecimal characters (lower case) of SHA-256 over the binary form of the user's SID, as
    /// <c>InstanceIdentity.HashOf</c> derives it.
    /// </summary>
    private static string SidHash()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid =
            identity.User
            ?? throw new InvalidOperationException("The current Windows identity has no user SID.");
        var binary = new byte[sid.BinaryLength];
        sid.GetBinaryForm(binary, 0);
        return Convert.ToHexStringLower(SHA256.HashData(binary))[..16];
    }
}
