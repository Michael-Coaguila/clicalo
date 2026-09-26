using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.Tools.InputProbe;

/// <summary>A command read from the pipe, or the description of why a line was rejected.</summary>
internal sealed record ProbeCommand(string Name, long? Id, nint? Window, string? Error)
{
    /// <summary>Parses one command line. Malformed lines become a command with <see cref="Error"/> set.</summary>
    public static ProbeCommand Parse(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (
                root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(ProbeCommands.CommandField, out var name)
                || name.ValueKind != JsonValueKind.String
            )
            {
                return Invalid("A command must be a JSON object with a string 'cmd' field.");
            }

            long? id =
                root.TryGetProperty(ProbeFields.Id, out var idElement)
                && idElement.TryGetInt64(out var idValue)
                    ? idValue
                    : null;
            nint? window =
                root.TryGetProperty(ProbeFields.Window, out var windowElement)
                && windowElement.TryGetInt64(out var windowValue)
                    ? (nint)windowValue
                    : null;
            return new ProbeCommand(name.GetString()!, id, window, Error: null);
        }
        catch (JsonException ex)
        {
            return Invalid("Malformed command: " + ex.Message);
        }
    }

    private static ProbeCommand Invalid(string error) =>
        new(string.Empty, Id: null, Window: null, error);
}
