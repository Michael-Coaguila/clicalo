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

            if (
                !TryReadOptionalInt64(root, ProbeFields.Id, out var id)
                || !TryReadOptionalInt64(root, ProbeFields.Window, out var window)
            )
            {
                return Invalid("The 'id' and 'hwnd' fields of a command must be integers.");
            }

            return new ProbeCommand(name.GetString()!, id, (nint?)window, Error: null);
        }
        catch (JsonException ex)
        {
            return Invalid("Malformed command: " + ex.Message);
        }
    }

    /// <summary>
    /// Reads an optional integer field. False when it is present with another type: <c>TryGetInt64</c> would throw
    /// on a non-number, and an exception here would end the command loop.
    /// </summary>
    private static bool TryReadOptionalInt64(JsonElement root, string name, out long? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var element))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number))
        {
            value = number;
            return true;
        }

        return false;
    }

    private static ProbeCommand Invalid(string error) =>
        new(string.Empty, Id: null, Window: null, error);
}
