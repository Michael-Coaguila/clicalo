using System.Collections.Immutable;
using System.Text.Json;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// Reads a v1 <c>profiles.json</c> into <see cref="V1Document"/> (blueprint §6.6, MIG-002): tolerates unknown keys,
/// a BOM, missing keys and truncated files as far as they parse; never executes anything.
/// </summary>
/// <remarks>
/// Every key is optional and its order free (catalog §7.2); a value of the wrong type counts as missing, so the
/// converter applies the v1 default. A repeated key behaves as it did in v1 (Python's <c>json</c>): the last value
/// wins, in the position of the first. A file that does not parse (empty, truncated, not UTF-8) or has no profiles is
/// a failure, never a partial import (EC-MIG-02). Size, depth, profiles and buttons are bounded (LOG-006).
/// </remarks>
public static class V1Reader
{
    private const string ActiveProfileKey = "active_profile";
    private const string PinnedProfileKey = "pinned_profile";
    private const string WindowPositionKey = "window_pos";
    private const string WindowSizeKey = "window_size";
    private const string EditSizeKey = "edit_size";
    private const string WindowOpacityKey = "window_opacity";
    private const string ButtonSizeKey = "button_size";
    private const string ProfilesKey = "profiles";

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        ActiveProfileKey,
        PinnedProfileKey,
        WindowPositionKey,
        WindowSizeKey,
        EditSizeKey,
        WindowOpacityKey,
        ButtonSizeKey,
        ProfilesKey,
    };

    private static ReadOnlySpan<byte> Bom => [0xEF, 0xBB, 0xBF];

    /// <summary>Reads the file bytes.</summary>
    /// <param name="utf8">The bytes exactly as on disk.</param>
    public static Result<V1Document> Read(ReadOnlySpan<byte> utf8)
    {
        if (utf8.StartsWith(Bom))
        {
            utf8 = utf8[Bom.Length..];
        }

        if (utf8.Length > Timings.Import.V1MaxBytes)
        {
            return Results.Fail<V1Document>(V1ImportFailures.TooLarge);
        }

        if (utf8.Trim(" \t\r\n"u8).IsEmpty)
        {
            return Results.Fail<V1Document>(V1ImportFailures.Empty);
        }

        var options = new JsonReaderOptions
        {
            MaxDepth = Timings.Import.V1MaxDepth,
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        try
        {
            var reader = new Utf8JsonReader(utf8, options);
            using var document = JsonDocument.ParseValue(ref reader);
            if (reader.Read())
            {
                // Something after the object: the file is not one JSON value.
                return Results.Fail<V1Document>(V1ImportFailures.Damaged);
            }

            return FromRoot(document.RootElement);
        }
        catch (JsonException)
        {
            return Results.Fail<V1Document>(V1ImportFailures.Damaged);
        }
        catch (InvalidOperationException)
        {
            // A string that is not valid UTF-8.
            return Results.Fail<V1Document>(V1ImportFailures.Damaged);
        }
    }

    private static Result<V1Document> FromRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Results.Fail<V1Document>(V1ImportFailures.NotV1);
        }

        var (order, values) = LastValueWins(root);
        var unknown = order.Where(static key => !KnownKeys.Contains(key)).ToImmutableArray();
        if (
            !values.TryGetValue(ProfilesKey, out var profilesElement)
            || profilesElement.ValueKind != JsonValueKind.Object
        )
        {
            return Results.Fail<V1Document>(V1ImportFailures.NoProfiles);
        }

        var (names, profileValues) = LastValueWins(profilesElement);
        if (names.Count == 0)
        {
            return Results.Fail<V1Document>(V1ImportFailures.NoProfiles);
        }

        if (names.Count > Timings.Import.ShareMaxProfiles)
        {
            return Results.Fail<V1Document>(V1ImportFailures.TooLarge);
        }

        var profiles = ImmutableArray.CreateBuilder<V1Profile>(names.Count);
        var buttons = 0;
        foreach (var name in names)
        {
            var profile = ReadProfile(name, profileValues[name]);
            buttons += profile.Buttons.Count;
            if (buttons > Timings.Import.ShareMaxShortcuts)
            {
                return Results.Fail<V1Document>(V1ImportFailures.TooLarge);
            }

            profiles.Add(profile);
        }

        return Results.Ok(
            new V1Document(
                String(values, ActiveProfileKey),
                String(values, PinnedProfileKey),
                Pair(values, WindowPositionKey),
                Pair(values, WindowSizeKey),
                Pair(values, EditSizeKey),
                Number(values, WindowOpacityKey),
                Pair(values, ButtonSizeKey),
                new ValueList<V1Profile>(profiles.MoveToImmutable()),
                new ValueList<string>(unknown)
            )
        );
    }

    private static V1Profile ReadProfile(string name, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new V1Profile(name, string.Empty, null, []);
        }

        var (_, values) = LastValueWins(element);
        var buttons = ImmutableArray.CreateBuilder<V1Button>();
        if (values.TryGetValue("buttons", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    buttons.Add(ReadButton(item));
                }
            }
        }

        return new V1Profile(
            name,
            String(values, "process") ?? string.Empty,
            Integer(values, "buttons_per_page"),
            new ValueList<V1Button>(buttons.ToImmutable())
        );
    }

    private static V1Button ReadButton(JsonElement element)
    {
        var (_, values) = LastValueWins(element);
        var label = Text(values, "label") ?? string.Empty;
        var type = String(values, "type");
        var hotkey = String(values, "hotkey");
        var action = String(values, "action");
        var color = String(values, "color");
        var kind = (type ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "" => hotkey is null && action is not null
                ? V1ButtonKind.ActionHotkey
                : V1ButtonKind.ImplicitHotkey,
            "HOTKEY" => hotkey is null && action is not null
                ? V1ButtonKind.ActionHotkey
                : V1ButtonKind.ExplicitHotkey,
            "URL" => V1ButtonKind.Url,
            "APP" => V1ButtonKind.App,
            "SEPARATOR" => V1ButtonKind.Separator,
            _ => V1ButtonKind.Unknown,
        };
        return kind switch
        {
            V1ButtonKind.Url or V1ButtonKind.App => new V1Button(
                kind,
                label,
                null,
                action,
                color,
                type
            ),
            V1ButtonKind.Separator => new V1Button(kind, label, null, null, color, type),

            // Hotkey kinds and unknown types: v1 sent «hotkey», or «action» when there was no «hotkey» (overlay.py).
            _ => new V1Button(kind, label, hotkey ?? action, null, color, type),
        };
    }

    /// <summary>The properties of an object with Python's semantics for repeated keys.</summary>
    private static (List<string> Order, Dictionary<string, JsonElement> Values) LastValueWins(
        JsonElement element
    )
    {
        var order = new List<string>();
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!values.ContainsKey(property.Name))
            {
                order.Add(property.Name);
            }

            values[property.Name] = property.Value;
        }

        return (order, values);
    }

    private static string? String(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A label as text even when it was written as a number (v1 showed it with <c>str</c>).</summary>
    private static string? Text(Dictionary<string, JsonElement> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        };
    }

    private static double? Number(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && double.IsFinite(number)
            ? number
            : null;

    private static int? Integer(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) ? Integer(value) : null;

    private static int? Integer(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (value.TryGetInt32(out var integer))
        {
            return integer;
        }

        // Qt wrote integers; a hand-edited 80.0 still means 80.
        return
            value.TryGetDouble(out var number)
            && double.IsFinite(number)
            && number is >= int.MinValue and <= int.MaxValue
            ? (int)Math.Round(number, MidpointRounding.AwayFromZero)
            : null;
    }

    private static V1Pair? Pair(Dictionary<string, JsonElement> values, string key)
    {
        if (
            !values.TryGetValue(key, out var value)
            || value.ValueKind != JsonValueKind.Array
            || value.GetArrayLength() < 2
        )
        {
            return null;
        }

        var first = Integer(value[0]);
        var second = Integer(value[1]);
        return first is { } x && second is { } y ? new V1Pair(x, y) : null;
    }
}
