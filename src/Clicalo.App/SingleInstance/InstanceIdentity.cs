using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Clicalo.App.SingleInstance;

/// <summary>
/// The names of the single instance (blueprint §3.4, ADR-0010, SIS-003): one instance per user and per session.
/// <c>sidHash</c> is the first 16 hexadecimal characters (lower case) of SHA-256 over the binary form of the user's
/// SID, so the names never reveal the SID and every tool (the Launcher in M5, a test) derives the same value. A start
/// with <c>--data &lt;folder&gt;</c> is its own instance (ADR-0030): <see cref="DataScope"/> adds to every name a
/// suffix derived from the full path of that folder, so a trial copy runs beside the person's Clícalo and never
/// reaches it.
/// </summary>
/// <param name="UserSid">The SID of the user running Clícalo.</param>
/// <param name="SidHash">16 hexadecimal characters derived from <paramref name="UserSid"/>.</param>
/// <param name="SessionId">The Windows session (WTS) of this process.</param>
internal sealed record InstanceIdentity(SecurityIdentifier UserSid, string SidHash, int SessionId)
{
    /// <summary>
    /// Empty for a normal start; for a start with <c>--data</c>, <c>.d</c> and 16 hexadecimal characters
    /// (<see cref="ScopeOf"/>).
    /// </summary>
    public string DataScope { get; init; } = string.Empty;

    /// <summary>The mutex that marks the running instance: <c>Local\Clicalo.{sidHash}{dataScope}.Instance</c>.</summary>
    public string MutexName => @"Local\Clicalo." + SidHash + DataScope + ".Instance";

    /// <summary>The pipe of the running instance, without the <c>\\.\pipe\</c> prefix.</summary>
    public string PipeName =>
        "Clicalo." + SidHash + DataScope + "." + SessionId.ToString(CultureInfo.InvariantCulture);

    /// <summary>The identity of the current process for the data folder of <paramref name="options"/>.</summary>
    /// <param name="options">The command line.</param>
    public static InstanceIdentity Current(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var identity = WindowsIdentity.GetCurrent();
        var sid =
            identity.User
            ?? throw new InvalidOperationException("The current Windows identity has no user SID.");
        using var process = Process.GetCurrentProcess();
        return new InstanceIdentity(sid, HashOf(sid), process.SessionId)
        {
            DataScope = options.IsolatedData ? ScopeOf(options.DataDirectory) : string.Empty,
        };
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

    /// <summary>
    /// The suffix of an isolated data folder: <c>.d</c> and the first 16 hexadecimal characters (lower case) of SHA-256
    /// over the UTF-8 of its full path, without a trailing separator and in upper case, because Windows paths ignore
    /// case. The same folder written in another way (relative, with <c>/</c>, <c>..</c> or another case) gives the
    /// same suffix, and the names never reveal the path.
    /// </summary>
    /// <param name="dataDirectory">The folder of <c>--data</c>.</param>
    public static string ScopeOf(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        var normalized = full.ToUpperInvariant();
        return ".d"
            + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }
}
