using System.Runtime.InteropServices;
using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// Where updates come from (ADR-0027): the GitHub Releases of the public repository, over HTTPS, with the two channels
/// of <c>vpk pack --channel</c>. The package id is fixed by ADR-0012.
/// </summary>
public static class UpdateChannels
{
    /// <summary>The public repository whose releases carry the packages.</summary>
    public const string Repository = "https://github.com/Michael-Coaguila/clicalo";

    /// <summary>The package id of <c>vpk pack</c>: the install folder is <c>%LocalAppData%\Clicalo.App</c>.</summary>
    public const string PackId = "Clicalo.App";

    /// <summary>The executable inside the package.</summary>
    public const string MainExe = "Clicalo.exe";

    /// <summary>
    /// The name of <paramref name="channel"/> in the packages of this process: <c>stable</c> or <c>beta</c> on x64 and
    /// <c>stable-arm64</c> or <c>beta-arm64</c> in an ARM64 process, whose packages <c>cl package --runtime
    /// win-arm64</c> writes in a feed of their own.
    /// </summary>
    /// <param name="channel">The channel of the settings.</param>
    public static string NameOf(UpdateChannel channel) =>
        NameOf(channel, RuntimeInformation.ProcessArchitecture);

    /// <summary>The name of <paramref name="channel"/> in the packages of <paramref name="architecture"/>.</summary>
    /// <param name="channel">The channel of the settings.</param>
    /// <param name="architecture">The architecture of the process.</param>
    public static string NameOf(UpdateChannel channel, Architecture architecture) =>
        (channel == UpdateChannel.Beta ? "beta" : "stable")
        + (architecture == Architecture.Arm64 ? "-arm64" : string.Empty);
}
