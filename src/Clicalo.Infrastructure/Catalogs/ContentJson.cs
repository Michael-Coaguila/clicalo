using System.Text.Json;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// Shared reading rules of the content files (<c>data/content</c>): strict JSON, texts per language and combinations
/// made only of catalog keys. Content is untrusted (LOG-006): anything outside these rules is rejected, never guessed.
/// </summary>
internal static class ContentJson
{
    private static readonly JsonDocumentOptions Strict = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32,
    };

    /// <summary>Parses <paramref name="json"/> and reads its root with <paramref name="read"/>; null when it fails.</summary>
    /// <typeparam name="T">What is read.</typeparam>
    /// <param name="json">The bytes of the file.</param>
    /// <param name="read">Reads the root; throws on a missing or mistyped property.</param>
    public static T? Read<T>(ReadOnlyMemory<byte> json, Func<JsonElement, T?> read)
        where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(json, Strict);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? read(document.RootElement)
                : null;
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
                        or ArgumentException
            )
        {
            return null;
        }
    }

    /// <summary>A text per language; every value must be a non-blank string.</summary>
    /// <param name="text">The JSON object.</param>
    /// <exception cref="FormatException">A value is not a non-blank string, or there is none.</exception>
    public static LocalizedText Text(JsonElement text)
    {
        var pairs = new List<KeyValuePair<LangCode, string>>();
        foreach (var entry in text.EnumerateObject())
        {
            var value =
                entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(entry.Name))
            {
                throw new FormatException("A localized text has an empty value.");
            }

            pairs.Add(KeyValuePair.Create(new LangCode(entry.Name), value));
        }

        return pairs.Count == 0
            ? throw new FormatException("A localized text has no language.")
            : new LocalizedText(pairs);
    }

    /// <summary>A non-empty string property.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <exception cref="FormatException">It is empty.</exception>
    public static string String(JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetString();
        return string.IsNullOrEmpty(value)
            ? throw new FormatException("Property " + name + " is empty.")
            : value;
    }

    /// <summary>
    /// The combination of a key array, or <see langword="null"/> when it is empty or holds a key outside the catalog
    /// (CAT-004).
    /// </summary>
    /// <param name="keys">The JSON array of key ids.</param>
    public static KeyChord? Chord(JsonElement keys)
    {
        var strokes = new List<KeyStroke>();
        foreach (var key in keys.EnumerateArray())
        {
            var id = new KeyId(key.GetString() ?? string.Empty);
            if (!KeyDefinitions.TryGet(id, out _))
            {
                return null;
            }

            strokes.Add(new KeyStroke(id));
        }

        var chord = KeyChord.Create(strokes);
        return chord.Strokes.IsEmpty ? null : chord;
    }
}
