using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Clicalo.Build;

/// <summary>
/// The options of <c>cl package</c>: the channel (<c>stable</c> by default, or <c>beta</c>), the version (by default
/// the <c>VersionPrefix</c> of Directory.Build.props, with <c>-beta.1</c> on the beta channel) and the runtime (by
/// default the one of this machine; <c>win-arm64</c> can be packaged from an x64 machine). The same options give the
/// same package (NFR-014).
/// </summary>
/// <param name="Channel">The channel of the settings: <c>stable</c> or <c>beta</c>.</param>
/// <param name="Version">The SemVer version of the package.</param>
/// <param name="Runtime">The runtime of the package: <c>win-x64</c> or <c>win-arm64</c>.</param>
internal sealed partial record PackageOptions(string Channel, string Version, string Runtime)
{
    /// <summary>The x64 runtime.</summary>
    public const string X64 = "win-x64";

    /// <summary>The ARM64 runtime.</summary>
    public const string Arm64 = "win-arm64";

    /// <summary>
    /// The Velopack channel of the package: the channel itself on x64 and <c>&lt;channel&gt;-arm64</c> on ARM64, so
    /// the packages of the two runtimes never share a feed (<c>UpdateChannels.NameOf</c> reads the same name).
    /// </summary>
    public string PackChannel =>
        string.Equals(Runtime, Arm64, StringComparison.Ordinal) ? Channel + "-arm64" : Channel;

    /// <summary>The stable channel.</summary>
    public const string Stable = "stable";

    /// <summary>The beta channel.</summary>
    public const string Beta = "beta";

    /// <summary>Reads the words after <c>cl package</c>.</summary>
    /// <param name="args">The words.</param>
    /// <param name="versionPrefix">The <c>VersionPrefix</c> of the repository.</param>
    /// <param name="machineRuntime">The runtime of this machine, the default.</param>
    /// <param name="options">The options, when they are valid.</param>
    /// <param name="error">Why they are not, with the usage.</param>
    public static bool TryParse(
        IReadOnlyList<string> args,
        string versionPrefix,
        string machineRuntime,
        [NotNullWhen(true)] out PackageOptions? options,
        out string error
    )
    {
        ArgumentNullException.ThrowIfNull(args);
        options = null;
        error = string.Empty;
        var channel = Stable;
        string? version = null;
        var runtime = machineRuntime;
        for (var i = 0; i < args.Count; i++)
        {
            var option = args[i];
            if (string.Equals(option, "--channel", StringComparison.Ordinal) && i + 1 < args.Count)
            {
                channel = args[++i].ToLowerInvariant();
            }
            else if (
                string.Equals(option, "--version", StringComparison.Ordinal)
                && i + 1 < args.Count
            )
            {
                version = args[++i];
            }
            else if (
                string.Equals(option, "--runtime", StringComparison.Ordinal)
                && i + 1 < args.Count
            )
            {
                runtime = args[++i].ToLowerInvariant();
            }
            else
            {
                error = Messages.PackageUnknownOption(option) + " " + Messages.PackageUsage;
                return false;
            }
        }

        if (channel is not (Stable or Beta))
        {
            error = Messages.PackageBadChannel(channel) + " " + Messages.PackageUsage;
            return false;
        }

        if (runtime is not (X64 or Arm64))
        {
            error = Messages.PackageBadRuntime(runtime) + " " + Messages.PackageUsage;
            return false;
        }

        version ??= string.Equals(channel, Beta, StringComparison.Ordinal)
            ? versionPrefix + "-beta.1"
            : versionPrefix;
        if (!SemVer().IsMatch(version))
        {
            error = Messages.PackageBadVersion(version) + " " + Messages.PackageUsage;
            return false;
        }

        options = new PackageOptions(channel, version, runtime);
        return true;
    }

    /// <summary>The <c>VersionPrefix</c> of <c>Directory.Build.props</c>.</summary>
    /// <param name="props">The text of the file.</param>
    public static string VersionPrefixOf(string props)
    {
        var match = Prefix().Match(props);
        return match.Success ? match.Groups["v"].Value.Trim() : "0.0.0";
    }

    [GeneratedRegex(
        @"^\d+\.\d+\.\d+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex SemVer();

    [GeneratedRegex(
        "<VersionPrefix>(?<v>[^<]+)</VersionPrefix>",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Prefix();
}
