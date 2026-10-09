using System.Text.Json;
using Clicalo.Domain.Keys;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// Reads the key labels of <c>data/catalogs/keys.json</c> (R-04): label, abbreviation for the size S and spoken name
/// of every catalog key, in each language. Untrusted data (LOG-006): an entry that breaks a rule is skipped, a file that
/// cannot be read gives <see langword="null"/>, and a key the generated catalog does not know is ignored.
/// </summary>
public static class KeyLabelsReader
{
    /// <summary>The file name inside the catalogs folder.</summary>
    public const string FileName = "keys.json";

    /// <summary>The labels of <paramref name="json"/>, or <see langword="null"/> when it is not a key catalog.</summary>
    /// <param name="json">The bytes of <c>keys.json</c>.</param>
    public static KeyLabelCatalog? Read(ReadOnlyMemory<byte> json) =>
        ContentJson.Read(json, static root => new KeyLabelCatalog(Entries(root)));

    private static List<KeyValuePair<KeyId, KeyLabel>> Entries(JsonElement root)
    {
        var entries = new List<KeyValuePair<KeyId, KeyLabel>>();
        foreach (var key in root.GetProperty("keys").EnumerateArray())
        {
            if (
                key.ValueKind != JsonValueKind.Object
                || !key.TryGetProperty("id", out var idElement)
                || idElement.GetString() is not { Length: > 0 } idText
                || !KeyDefinitions.TryGet(new KeyId(idText), out _)
                || !key.TryGetProperty("label", out var label)
            )
            {
                continue;
            }

            entries.Add(
                KeyValuePair.Create(
                    new KeyId(idText),
                    new KeyLabel(
                        ContentJson.Text(label),
                        Optional(key, "short"),
                        Optional(key, "spoken")
                    )
                )
            );
        }

        return entries;
    }

    private static Domain.Primitives.LocalizedText? Optional(JsonElement key, string name) =>
        key.TryGetProperty(name, out var value) ? ContentJson.Text(value) : null;
}
