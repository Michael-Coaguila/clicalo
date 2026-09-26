using System.Reflection;
using System.Runtime.InteropServices;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>The machine and build the report was produced on (no user or machine names).</summary>
/// <param name="Windows">Windows version and build (<see cref="Environment.OSVersion"/>).</param>
/// <param name="Architecture">Process architecture.</param>
/// <param name="LabVersion">Informational version of SpikeLab, with the commit when the build knows it.</param>
internal sealed record MachineInfo(string Windows, string Architecture, string LabVersion)
{
    /// <summary>The current machine and build.</summary>
    public static MachineInfo Current() =>
        new(
            Environment.OSVersion.VersionString,
            RuntimeInformation.ProcessArchitecture.ToString(),
            typeof(MachineInfo)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? "unknown"
        );
}
