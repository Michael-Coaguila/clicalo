using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>
/// The machine and build the report was produced on (no user or machine names): the Windows build with its update
/// revision, the monitors with resolution and scale, and the touch, pen and mouse hardware, which is what the header
/// of the manual results of S1.md asks for.
/// </summary>
/// <param name="Windows">Windows build with its update revision and version name («10.0.26200.6584 (25H2)»).</param>
/// <param name="Architecture">Process architecture.</param>
/// <param name="LabVersion">Informational version of SpikeLab, with the commit when the build knows it.</param>
internal sealed record MachineInfo(string Windows, string Architecture, string LabVersion)
{
    /// <summary>The Windows build with its update revision (UBR), «10.0.26200.6584».</summary>
    public string? WindowsBuild { get; init; }

    /// <summary>The Windows version name, «25H2»; null when the registry does not say.</summary>
    public string? WindowsDisplayVersion { get; init; }

    /// <summary>The monitors, the primary first.</summary>
    public ImmutableArray<MonitorDescription> Monitors { get; init; } = [];

    /// <summary>Touch, pen and mouse.</summary>
    public InputHardware Input { get; init; } = InputHardware.Unknown;

    /// <summary>The current machine and build.</summary>
    public static MachineInfo Current()
    {
        var (build, displayVersion) = HardwareProbe.WindowsVersion();
        return new MachineInfo(
            displayVersion is null ? build : build + " (" + displayVersion + ")",
            RuntimeInformation.ProcessArchitecture.ToString(),
            typeof(MachineInfo)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? "unknown"
        )
        {
            WindowsBuild = build,
            WindowsDisplayVersion = displayVersion,
            Monitors = HardwareProbe.Monitors(),
            Input = HardwareProbe.Input(),
        };
    }
}
