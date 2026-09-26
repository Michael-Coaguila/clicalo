using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.Tools.InputProbe;

/// <summary>Validated command line of the probe: <c>--pipe &lt;name&gt;</c> and nothing else.</summary>
internal sealed record ProbeArguments(string PipeName)
{
    private const int MaxPipeNameLength = 200;

    /// <summary>Parses <paramref name="args"/>; returns null when they are missing, unknown or malformed.</summary>
    public static ProbeArguments? TryParse(IReadOnlyList<string> args)
    {
        string? pipeName = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (
                pipeName is null
                && string.Equals(args[i], ProbeProtocol.PipeSwitch, StringComparison.Ordinal)
                && i + 1 < args.Count
            )
            {
                pipeName = args[++i];
                continue;
            }

            return null;
        }

        return IsValidPipeName(pipeName) ? new ProbeArguments(pipeName) : null;
    }

    private static bool IsValidPipeName(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? name
    ) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= MaxPipeNameLength
        && !name.Contains('\\', StringComparison.Ordinal)
        && !name.Contains('/', StringComparison.Ordinal);
}
