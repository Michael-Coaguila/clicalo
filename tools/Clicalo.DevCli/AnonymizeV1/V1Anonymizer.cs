using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Clicalo.DevCli.AnonymizeV1;

/// <summary>
/// Turns a real Macro Quick Access <c>profiles.json</c> into a fixture without personal data (blueprint §6.6,
/// M2-ownership «Privacidad de los fixtures»). Kept as they are: the structure and key order, every combination
/// (<c>hotkey</c>, and <c>action</c> when it is a combination), colours, types, numbers and counts. Kept only when
/// public (<see cref="PublicNames"/>): profile and button names and program names. Everything else that is text
/// (web addresses after the scheme, app commands, <c>_nota</c>, unknown keys) becomes a placeholder of the same length
/// and type (<see cref="Placeholders"/>). The result is checked before it is returned: same skeleton, and no replaced
/// text left anywhere. One instance anonymizes one file.
/// </summary>
/// <param name="publicNames">What may be kept.</param>
internal sealed class V1Anonymizer(PublicNames publicNames)
{
    private const string ProfilesKey = "profiles";

    /// <summary>The longest switch kept as written, without its dashes or slash (<c>/min</c>).</summary>
    private const int MaxKeptSwitchLength = 3;

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private static readonly HashSet<string> SettingKeys = new(StringComparer.Ordinal)
    {
        "window_pos",
        "window_size",
        "edit_size",
        "window_opacity",
        "button_size",
    };

    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".com",
        ".bat",
        ".cmd",
        ".ps1",
        ".vbs",
        ".js",
        ".wsf",
        ".hta",
        ".py",
        ".lnk",
        ".url",
        ".msc",
        ".txt",
        ".pdf",
        ".docx",
        ".xlsx",
        ".pptx",
        ".png",
        ".jpg",
    };

    private static readonly SearchValues<char> SwitchChars = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-"
    );

    private static readonly SearchValues<char> SchemeChars = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+.-"
    );

    private readonly Placeholders _placeholders = new(publicNames.Names.Contains);
    private int _keptNames;

    /// <summary>How JSON is written: two-space indentation, LF, non-ASCII kept (like v1's <c>ensure_ascii=False</c>).</summary>
    public static JsonWriterOptions WriterOptions { get; } =
        new()
        {
            Indented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

    /// <summary>Anonymizes one v1 file.</summary>
    /// <param name="input">The file bytes.</param>
    /// <exception cref="InvalidDataException">The input is not a v1 file (not a JSON object).</exception>
    /// <exception cref="InvalidOperationException">The self-check failed; nothing must be written.</exception>
    public (byte[] Output, AnonymizeStats Stats) Anonymize(ReadOnlySpan<byte> input)
    {
        var hasBom = input.StartsWith(Bom);
        var json = hasBom ? input[Bom.Length..] : input;
        JsonObject original;
        try
        {
            original =
                JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidDataException("The input is not a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The input is not valid JSON.", ex);
        }

        var anonymized = new JsonObject();
        try
        {
            foreach (var (key, value) in original)
            {
                anonymized[key] = key switch
                {
                    ProfilesKey => Profiles(value),
                    "active_profile" or "pinned_profile" => Name(value),
                    _ when SettingKeys.Contains(key) => value?.DeepClone(),
                    _ => Scrub(value),
                };
            }
        }
        catch (ArgumentException ex)
        {
            // JsonNode refuses a repeated key when the object is first read: the fixture must be faithful.
            throw new InvalidDataException("The input repeats a key.", ex);
        }

        var bytes = Write(anonymized, hasBom);
        Verify(original, bytes);
        var profiles = (original[ProfilesKey] as JsonObject)?.Count ?? 0;
        var buttons =
            (original[ProfilesKey] as JsonObject)
                ?.Select(static p => (p.Value?["buttons"] as JsonArray)?.Count ?? 0)
                .Sum()
            ?? 0;
        return (bytes, new AnonymizeStats(profiles, buttons, _keptNames, CountReplaced()));
    }

    /// <summary>
    /// A text that says what a document looks like without its private text: structure, key order, lengths of the
    /// hidden texts and the exact kept values (combinations, colours, types, numbers).
    /// </summary>
    /// <param name="node">A v1 document.</param>
    internal static string Skeleton(JsonNode? node)
    {
        var builder = new StringBuilder();
        AppendSkeleton(builder, node, parentKey: null, isProfileMap: false);
        return builder.ToString();
    }

    private static void AppendSkeleton(
        StringBuilder builder,
        JsonNode? node,
        string? parentKey,
        bool isProfileMap
    )
    {
        switch (node)
        {
            case JsonObject map:
                builder.Append('{');
                foreach (var (key, value) in map)
                {
                    // Profile names are hidden or kept: only their length is structure.
                    builder.Append(isProfileMap ? "#" + key.Length : key).Append(':');
                    AppendSkeleton(
                        builder,
                        value,
                        key,
                        string.Equals(key, ProfilesKey, StringComparison.Ordinal) && !isProfileMap
                    );
                    builder.Append(',');
                }

                builder.Append('}');
                break;
            case JsonArray list:
                builder.Append('[');
                foreach (var item in list)
                {
                    AppendSkeleton(builder, item, parentKey, isProfileMap: false);
                    builder.Append(',');
                }

                builder.Append(']');
                break;
            case JsonValue value when value.TryGetValue<string>(out var text):
                builder.Append(
                    parentKey is "hotkey" or "color" or "type" ? "=" + text : "#" + text.Length
                );
                break;
            case null:
                builder.Append("null");
                break;
            default:
                builder.Append(node.ToJsonString());
                break;
        }
    }

    /// <summary>
    /// A short command-line switch such as <c>/c</c>, <c>/k</c> or <c>-c</c>: not personal, and it shows the kind of
    /// command. A longer one (<c>--new-window</c>, but also <c>-juan</c>) may carry a name, so it is replaced like any
    /// other word and keeps only its shape.
    /// </summary>
    private static bool IsSwitch(string word)
    {
        var start = word.StartsWith("--", StringComparison.Ordinal) ? 2 : 1;
        return word.Length > start
            && word.Length - start <= MaxKeptSwitchLength
            && word[0] is '-' or '/'
            && char.IsAsciiLetter(word[start])
            && !word.AsSpan(start + 1).ContainsAnyExcept(SwitchChars);
    }

    /// <summary>The length of the scheme of an address (<c>https://</c>), or 0.</summary>
    private static int SchemeLength(string address)
    {
        if (address.Length == 0 || !char.IsAsciiLetter(address[0]))
        {
            return 0;
        }

        var colon = address.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || address.AsSpan(1, colon - 1).ContainsAnyExcept(SchemeChars))
        {
            return 0;
        }

        return address.AsSpan(colon + 1).StartsWith("//", StringComparison.Ordinal)
            ? colon + 3
            : colon + 1;
    }

    private static byte[] Write(JsonObject document, bool bom)
    {
        using var buffer = new MemoryStream();
        if (bom)
        {
            buffer.Write(Bom);
        }

        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            document.WriteTo(writer);
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    private static IEnumerable<string> Strings(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject map:
                foreach (var (key, value) in map)
                {
                    yield return key;
                    foreach (var text in Strings(value))
                    {
                        yield return text;
                    }
                }

                break;
            case JsonArray list:
                foreach (var item in list)
                {
                    foreach (var text in Strings(item))
                    {
                        yield return text;
                    }
                }

                break;
            case JsonValue value when value.TryGetValue<string>(out var text):
                yield return text;
                break;
        }
    }

    private int CountReplaced() =>
        _placeholders.Originals.Count(o =>
            !string.Equals(_placeholders.For(o), o, StringComparison.Ordinal)
        );

    /// <summary>The self-check: the same skeleton, and no replaced text anywhere in the output.</summary>
    private void Verify(JsonObject original, byte[] bytes)
    {
        var output = JsonNode.Parse(bytes.AsSpan(bytes.AsSpan().StartsWith(Bom) ? Bom.Length : 0));
        if (!string.Equals(Skeleton(original), Skeleton(output), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The anonymized file lost structure, combinations or colours."
            );
        }

        var present = Strings(output).ToHashSet(StringComparer.Ordinal);
        foreach (var hidden in _placeholders.Originals)
        {
            if (
                !string.Equals(_placeholders.For(hidden), hidden, StringComparison.Ordinal)
                && present.Contains(hidden)
            )
            {
                throw new InvalidOperationException(
                    "A replaced text is still in the anonymized file."
                );
            }
        }
    }

    private JsonNode? Profiles(JsonNode? node)
    {
        if (node is not JsonObject profiles)
        {
            return Scrub(node);
        }

        var result = new JsonObject();
        foreach (var (name, profile) in profiles)
        {
            result[NameText(name)] = Profile(profile);
        }

        return result;
    }

    private JsonNode? Profile(JsonNode? node)
    {
        if (node is not JsonObject profile)
        {
            return Scrub(node);
        }

        var result = new JsonObject();
        foreach (var (key, value) in profile)
        {
            result[key] = key switch
            {
                "process" => Map(value, Program),
                "buttons_per_page" => value?.DeepClone(),
                "buttons" when value is JsonArray buttons => new JsonArray([
                    .. buttons.Select(Button),
                ]),
                _ => Scrub(value),
            };
        }

        return result;
    }

    private JsonNode? Button(JsonNode? node)
    {
        if (node is not JsonObject button)
        {
            return Scrub(node);
        }

        var type =
            button["type"] is JsonValue t && t.TryGetValue<string>(out var text)
                ? text.Trim().ToUpperInvariant()
                : string.Empty;
        var result = new JsonObject();
        foreach (var (key, value) in button)
        {
            result[key] = key switch
            {
                "label" => Name(value),
                "hotkey" or "color" or "type" => value?.DeepClone(),
                "action" when type is "URL" => Map(value, Url),
                "action" when type is "APP" => Map(value, App),

                // Any other type sends «action» as a combination (variant c): kept like «hotkey».
                "action" when type is not "SEPARATOR" => value?.DeepClone(),
                _ => Scrub(value),
            };
        }

        return result;
    }

    private JsonNode? Name(JsonNode? node) => Map(node, NameText);

    private string NameText(string name)
    {
        if (name.Length == 0 || publicNames.Names.Contains(name))
        {
            _keptNames += name.Length == 0 ? 0 : 1;
            return name;
        }

        return _placeholders.For(name);
    }

    private string Program(string program)
    {
        if (program.Length == 0 || publicNames.IsPublicProgram(program))
        {
            return program;
        }

        return KeepExtension(program);
    }

    private string Url(string address)
    {
        var scheme = SchemeLength(address);
        return scheme > 0
            ? address[..scheme] + _placeholders.For(address[scheme..])
            : _placeholders.For(address);
    }

    /// <summary>
    /// A command, word by word: public program names and switches stay, the folders before a public program and
    /// every other word become placeholders (known extensions stay, so the kind of target is still visible).
    /// </summary>
    private string App(string command)
    {
        var result = new StringBuilder(command.Length);
        var word = new StringBuilder();
        foreach (var c in command)
        {
            if (char.IsWhiteSpace(c))
            {
                result.Append(Word(word.ToString())).Append(c);
                word.Clear();
            }
            else
            {
                word.Append(c);
            }
        }

        return result.Append(Word(word.ToString())).ToString();
    }

    private string Word(string word)
    {
        if (word.Length == 0 || IsSwitch(word))
        {
            return word;
        }

        var bare = word.Trim('"');
        var slash = bare.LastIndexOfAny(['\\', '/']);
        var fileName = slash < 0 ? bare : bare[(slash + 1)..];
        if (!publicNames.IsPublicProgram(fileName))
        {
            return KeepExtension(word);
        }

        if (slash < 0)
        {
            return word;
        }

        var start = word.IndexOf(bare, StringComparison.Ordinal);
        return word[..start]
            + _placeholders.For(bare[..(slash + 1)])
            + fileName
            + word[(start + bare.Length)..];
    }

    private string KeepExtension(string text)
    {
        var trimmed = text.TrimEnd('"');
        var dot = trimmed.LastIndexOf('.');
        if (dot > 0 && KnownExtensions.Contains(trimmed[dot..]))
        {
            return _placeholders.For(text[..dot]) + text[dot..];
        }

        return _placeholders.For(text);
    }

    private JsonNode? Scrub(JsonNode? node) =>
        node switch
        {
            JsonObject map => new JsonObject(
                map.Select(p => KeyValuePair.Create(p.Key, Scrub(p.Value)))
            ),
            JsonArray list => new JsonArray([.. list.Select(Scrub)]),
            JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(
                _placeholders.For(text)
            ),
            _ => node?.DeepClone(),
        };

    private static JsonNode? Map(JsonNode? node, Func<string, string> map) =>
        node is JsonValue value && value.TryGetValue<string>(out var text)
            ? JsonValue.Create(map(text))
            : node?.DeepClone();
}
