using System.Runtime.InteropServices;

namespace Clicalo.Performance;

/// <summary>Where a measurement ran: enough to compare runs, nothing that identifies a person.</summary>
/// <param name="Runner">A GitHub runner name (<c>RUNNER_NAME</c>), or «local».</param>
/// <param name="Architecture">x64 or arm64.</param>
/// <param name="LogicalProcessors">Logical processors.</param>
/// <param name="SendInput">Whether Clicalo.exe ran with key sending (only in the CI).</param>
internal sealed record MeasurementContext(
    string Runner,
    string Architecture,
    int LogicalProcessors,
    bool SendInput
)
{
    /// <summary>The context of this process.</summary>
    /// <param name="sendInput">Whether Clicalo.exe runs with key sending.</param>
    public static MeasurementContext Current(bool sendInput) =>
        new(
            Environment.GetEnvironmentVariable("RUNNER_NAME") is { Length: > 0 } runner
                ? runner
                : "local",
            RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            Environment.ProcessorCount,
            sendInput
        );
}
