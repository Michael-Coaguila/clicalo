using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Clicalo.App.SingleInstance;

/// <summary>
/// The names of the single instance (blueprint §3.4, ADR-0010, SIS-003): one instance per user and per session.
/// <c>sidHash</c> is the first 16 hexadecimal characters (lower case) of SHA-256 over the binary form of the user's
/// SID, so the names never reveal the SID and every tool (the Launcher in M5, a test) derives the same value.
/// </summary>
/// <param name="UserSid">The SID of the user running Clícalo.</param>
/// <param name="SidHash">16 hexadecimal characters derived from <paramref name="UserSid"/>.</param>
/// <param name="SessionId">The Windows session (WTS) of this process.</param>
internal sealed record InstanceIdentity(SecurityIdentifier UserSid, string SidHash, int SessionId)
{
    /// <summary>The mutex that marks the running instance: <c>Local\Clicalo.{sidHash}.Instance</c>.</summary>
    public string MutexName => @"Local\Clicalo." + SidHash + ".Instance";

    /// <summary>The pipe of the running instance, without the <c>\\.\pipe\</c> prefix.</summary>
    public string PipeName =>
        "Clicalo." + SidHash + "." + SessionId.ToString(CultureInfo.InvariantCulture);

    /// <summary>The identity of the current process.</summary>
    public static InstanceIdentity Current()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid =
            identity.User
            ?? throw new InvalidOperationException("The current Windows identity has no user SID.");
        using var process = Process.GetCurrentProcess();
        return new InstanceIdentity(sid, HashOf(sid), process.SessionId);
    }

    /// <summary>The <c>sidHash</c> of <paramref name="sid"/>.</summary>
    /// <param name="sid">A user SID.</param>
    public static string HashOf(SecurityIdentifier sid)
    {
        ArgumentNullException.ThrowIfNull(sid);
        var binary = new byte[sid.BinaryLength];
        sid.GetBinaryForm(binary, 0);
        return Convert.ToHexStringLower(SHA256.HashData(binary))[..16];
    }
}
