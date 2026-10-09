using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Clicalo.Build;

/// <summary>
/// The options of <c>cl package</c>: the channel (<c>stable</c> by default, or <c>beta</c>) and the version (by default
/// the <c>VersionPrefix</c> of Directory.Build.props, with <c>-beta.1</c> on the beta channel). The same options give
/// the same package (NFR-014).
/// </summary>
/// <param name="Channel">The Velopack channel.</param>
/// <param name="Version">The SemVer version of the package.</param>
internal sealed partial record PackageOptions(string Channel, string Version)
{
    /// <summary>The stable channel.</summary>
    public const string Stable = "stable";

    /// <summary>The beta channel.</summary>
    public const string Beta = "beta";

    /// <summary>Reads the words after <c>cl package</c>.</summary>
    /// <param name="args">The words.</param>
    /// <param name="versionPrefix">The <c>VersionPrefix</c> of the repository.</param>
    /// <param name="options">The options, when they are valid.</param>
    /// <param name="error">Why they are not, with the usage.</param>
    public static bool TryParse(
        IReadOnlyList<string> args,
        string versionPrefix,
        [NotNullWhen(true)] out PackageOptions? options,
        out string error
    )
    {
        ArgumentNullException.ThrowIfNull(args);
        options = null;
        error = string.Empty;
        var channel = Stable;
        string? version = null;
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

        version ??= string.Equals(channel, Beta, StringComparison.Ordinal)
            ? versionPrefix + "-beta.1"
            : versionPrefix;
        if (!SemVer().IsMatch(version))
        {
            error = Messages.PackageBadVersion(version) + " " + Messages.PackageUsage;
            return false;
        }

        options = new PackageOptions(channel, version);
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
