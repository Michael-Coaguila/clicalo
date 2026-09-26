using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Clicalo.DevCli.Adr;

/// <summary>Reads <c>architecture/sensitive-paths.json</c> (blueprint §13).</summary>
internal static class SensitivePaths
{
    /// <summary>Repository-relative path of the registry.</summary>
    public const string FileName = "architecture/sensitive-paths.json";

    /// <summary>Parses the registry.</summary>
    /// <exception cref="InvalidDataException">The registry is not valid; the message says where.</exception>
    public static ImmutableArray<SensitivePath> Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(FileName + ": invalid JSON: " + ex.Message, ex);
        }

        using (document)
        {
            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("paths", out var paths)
                || paths.ValueKind != JsonValueKind.Array
            )
            {
                throw new InvalidDataException(FileName + ": the root needs a 'paths' array.");
            }

            var builder = ImmutableArray.CreateBuilder<SensitivePath>();
            foreach (var entry in paths.EnumerateArray())
            {
                var index = builder.Count;
                builder.Add(
                    new SensitivePath(
                        new PathGlob(Required(entry, "pattern", index)),
                        Required(entry, "category", index),
                        Required(entry, "reason", index)
                    )
                );
            }

            return builder.ToImmutable();
        }
    }

    private static string Required(JsonElement entry, string property, int index) =>
        entry.ValueKind == JsonValueKind.Object
        && entry.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidDataException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{FileName}: paths[{index}] needs a non-empty '{property}'."
                )
            );
}
