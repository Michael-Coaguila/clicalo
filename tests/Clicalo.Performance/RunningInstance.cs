using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Clicalo.Performance;

/// <summary>
/// The single instance of Clícalo in this session, seen from outside the process (App/SingleInstance/InstanceIdentity,
/// blueprint §3.4, SIS-003): the names of its mutex and of its pipe, and whether one is running. Every measurement
/// starts <c>Clicalo.exe</c> as a process, and there is one instance per user and session, so a start is only a first
/// start when no other instance is alive: not one of another measurement (this assembly runs its tests one at a time)
/// and not the Clícalo the person uses.
/// </summary>
internal static class RunningInstance
{
    /// <summary>The longest wait for the previous instance of a measurement to be gone after it was ended.</summary>
    public static readonly TimeSpan GoneTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The mutex that marks the running instance: <c>Local\Clicalo.{sidHash}.Instance</c>.</summary>
    public static string MutexName => @"Local\Clicalo." + SidHash() + ".Instance";

    /// <summary>The pipe of the running instance, without the <c>\\.\pipe\</c> prefix.</summary>
    public static string PipeName
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return "Clicalo."
                + SidHash()
                + "."
                + process.SessionId.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Whether a Clícalo of this user and session is running now.</summary>
    public static bool Exists()
    {
        if (!Mutex.TryOpenExisting(MutexName, out var mutex))
        {
            return false;
        }

        mutex.Dispose();
        return true;
    }

    /// <summary>
    /// Waits until no Clícalo of this session is running; the process a measurement just ended may need a moment to
    /// close its handles.
    /// </summary>
    /// <param name="limit">The longest wait.</param>
    /// <exception cref="InvalidOperationException">An instance is still running after <paramref name="limit"/>.</exception>
    public static void WaitUntilGone(TimeSpan limit)
    {
        if (!SpinWait.SpinUntil(static () => !Exists(), limit))
        {
            throw new InvalidOperationException(
                "Another Clícalo of this session is running and is not one this measurement started: every start "
                    + "would only show that instance (exit code 0, 2 or 3). Close it (Salir, in the tray) and run "
                    + "«cl perf» again."
            );
        }
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
