using System.Collections.Immutable;
using System.Text.Json;

namespace Clicalo.DevCli.I18n;

/// <summary>
/// The reviewed conversion from the design handoff to <c>data/i18n</c> (<c>data/i18n/handoff-import.json</c>).
/// Strict: unknown properties are errors, so a typo never silently changes the import. <see cref="Retired"/> lists the
/// handoff keys a user decision removed from the product: they are not imported and need a reason in <c>notes</c>.
/// </summary>
internal sealed record HandoffRecipe(
    string Source,
    ImmutableArray<string> Languages,
    ImmutableDictionary<string, string> Placeholders,
    ImmutableDictionary<string, HandoffKeyRule> Keys,
    ImmutableArray<HandoffAddedKey> Added,
    ImmutableHashSet<string> Retired
)
{
    /// <summary>Reads the recipe.</summary>
    /// <exception cref="InvalidDataException">The recipe is not valid; the message says where.</exception>
    public static HandoffRecipe Parse(string json)
    {
        using var document = ParseDocument(json);
        var root = document.RootElement;
        Expect(root, JsonValueKind.Object, "the root");
        AllowOnly(
            root,
            "the root",
            "notes",
            "source",
            "languages",
            "placeholders",
            "keys",
            "added",
            "retired"
        );
        var languages = Required(root, "languages", "the root")
            .EnumerateArray()
            .Select(static l =>
                l.GetString() ?? throw Invalid("'languages' must list language codes.")
            )
            .ToImmutableArray();
        if (languages.IsEmpty)
        {
            throw Invalid("'languages' must not be empty.");
        }

        var keys = ImmutableDictionary.CreateBuilder<string, HandoffKeyRule>(
            StringComparer.Ordinal
        );
        if (root.TryGetProperty("keys", out var keysElement))
        {
            foreach (var key in Objects(keysElement, "'keys'"))
            {
                keys.Add(key.Name, ParseRule(key.Name, key.Value));
            }
        }

        var added = ImmutableArray.CreateBuilder<HandoffAddedKey>();
        if (root.TryGetProperty("added", out var addedElement))
        {
            foreach (var key in Objects(addedElement, "'added'"))
            {
                var owner = "added." + key.Name;
                AllowOnly(key.Value, owner, "notes", "after", "text", "plural");
                var hasText = key.Value.TryGetProperty("text", out var text);
                var hasPlural = key.Value.TryGetProperty("plural", out var plural);
                if (hasText == hasPlural)
                {
                    throw Invalid(owner + " needs exactly one of 'text' or 'plural'.");
                }

                added.Add(
                    new HandoffAddedKey(
                        key.Name,
                        String(Required(key.Value, "after", owner), owner + ".after"),
                        hasText
                            ? Strings(text, owner + ".text")
                            : ImmutableDictionary.Create<string, string>(StringComparer.Ordinal),
                        hasPlural
                            ? Forms(plural, owner + ".plural")
                            : ImmutableDictionary.Create<
                                string,
                                ImmutableDictionary<string, string>
                            >(StringComparer.Ordinal)
                    )
                );
            }
        }

        var retired = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("retired", out var retiredElement))
        {
            foreach (var key in Objects(retiredElement, "'retired'"))
            {
                var owner = "retired." + key.Name;
                AllowOnly(key.Value, owner, "notes");
                _ = String(Required(key.Value, "notes", owner), owner + ".notes");
                retired.Add(key.Name);
            }
        }

        return new HandoffRecipe(
            String(Required(root, "source", "the root"), "source"),
            languages,
            Strings(Required(root, "placeholders", "the root"), "placeholders"),
            keys.ToImmutable(),
            added.ToImmutable(),
            retired.ToImmutable()
        );
    }

    private static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid JSON: " + ex.Message, ex);
        }
    }

    private static HandoffKeyRule ParseRule(string key, JsonElement element)
    {
        var owner = "keys." + key;
        AllowOnly(element, owner, "notes", "placeholders", "text", "plural", "arguments");
        var rule = new HandoffKeyRule();
        if (element.TryGetProperty("placeholders", out var placeholders))
        {
            rule = rule with { Placeholders = Strings(placeholders, owner + ".placeholders") };
        }

        if (element.TryGetProperty("text", out var text))
        {
            rule = rule with { Text = Strings(text, owner + ".text") };
        }

        if (element.TryGetProperty("plural", out var plural))
        {
            rule = rule with { Plural = Forms(plural, owner + ".plural") };
        }

        if (element.TryGetProperty("arguments", out var arguments))
        {
            var builder = ImmutableDictionary.CreateBuilder<string, HandoffComposedArgument>(
                StringComparer.Ordinal
            );
            foreach (var argument in Objects(arguments, owner + ".arguments"))
            {
                var where = owner + ".arguments." + argument.Name;
                AllowOnly(argument.Value, where, "message", "count");
                builder.Add(
                    argument.Name,
                    new HandoffComposedArgument(
                        String(Required(argument.Value, "message", where), where + ".message"),
                        String(Required(argument.Value, "count", where), where + ".count")
                    )
                );
            }

            rule = rule with { Arguments = builder.ToImmutable() };
        }

        return rule;
    }

    private static ImmutableDictionary<string, ImmutableDictionary<string, string>> Forms(
        JsonElement element,
        string owner
    )
    {
        var builder = ImmutableDictionary.CreateBuilder<
            string,
            ImmutableDictionary<string, string>
        >(StringComparer.Ordinal);
        foreach (var language in Objects(element, owner))
        {
            builder.Add(language.Name, Strings(language.Value, owner + "." + language.Name));
        }

        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, string> Strings(JsonElement element, string owner)
    {
        Expect(element, JsonValueKind.Object, owner);
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            builder.Add(property.Name, String(property.Value, owner + "." + property.Name));
        }

        return builder.ToImmutable();
    }

    private static IEnumerable<JsonProperty> Objects(JsonElement element, string owner)
    {
        Expect(element, JsonValueKind.Object, owner);
        foreach (
            var property in element
                .EnumerateObject()
                .Where(static p => !string.Equals(p.Name, "notes", StringComparison.Ordinal))
        )
        {
            Expect(property.Value, JsonValueKind.Object, owner + "." + property.Name);
            yield return property;
        }
    }

    private static JsonElement Required(JsonElement element, string property, string owner) =>
        element.TryGetProperty(property, out var value)
            ? value
            : throw Invalid(owner + " needs the property '" + property + "'.");

    private static string String(JsonElement element, string owner) =>
        element.ValueKind == JsonValueKind.String && element.GetString() is { Length: > 0 } value
            ? value
            : throw Invalid(owner + " must be a non-empty string.");

    private static void Expect(JsonElement element, JsonValueKind kind, string owner)
    {
        if (element.ValueKind != kind)
        {
            throw Invalid(owner + " must be a JSON " + kind.ToString().ToLowerInvariant() + ".");
        }
    }

    private static void AllowOnly(JsonElement element, string owner, params string[] allowed)
    {
        foreach (
            var property in element
                .EnumerateObject()
                .Where(p => !allowed.Contains(p.Name, StringComparer.Ordinal))
        )
        {
            throw Invalid("Unknown property '" + property.Name + "' in " + owner + ".");
        }
    }

    private static InvalidDataException Invalid(string message) =>
        new(I18nPaths.RecipeFileName + ": " + message);
}
