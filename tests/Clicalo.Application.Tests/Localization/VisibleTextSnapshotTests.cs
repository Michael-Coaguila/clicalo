using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Tests.Localization;

/// <summary>
/// Binding condition of the i18n conversion (blueprint §1.3 and §8.5, ADR-0011): for every key of the design handoff
/// and every language, formatting the converted text with sample arguments gives exactly the text of the handoff with
/// its one-letter markers replaced by the same values.
/// </summary>
/// <remarks>
/// The letter → name mapping comes from the recipe itself, so this test only proves that data/i18n and the recipe
/// agree: a recipe that maps a letter to the wrong name passes it. Whether each name is the right one is checked
/// independently, against hand-written texts, by <see cref="HandoffTextGoldenTests"/>.
/// </remarks>
public sealed partial class VisibleTextSnapshotTests
{
    private static readonly Dictionary<string, string> TextSamples = new(StringComparer.Ordinal)
    {
        ["app"] = "Word",
        ["profile"] = "Informes",
        ["name"] = "Negrita",
        ["keys"] = "Ctrl + B",
        ["version"] = "2.0.1",
    };

    private static readonly Dictionary<string, long> NumberSamples = new(StringComparer.Ordinal)
    {
        ["count"] = 3,
        ["index"] = 2,
        ["total"] = 5,
        ["profiles"] = 4,
        ["shortcuts"] = 210,
    };

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "IDI-001")]
    [Trait("Req", "IDI-004")]
    public void Every_handoff_text_is_shown_identically_with_the_sample_arguments(string language)
    {
        var localizer = I18nRepository.Localizer(language);
        using var recipe = I18nRepository.Recipe();
        var handoff = I18nRepository.Handoff(language);
        var mismatches = new List<string>();

        foreach (var (key, original) in handoff)
        {
            var conversion = Conversion.For(recipe.RootElement, key);
            var expected = Marker()
                .Replace(original, m => SampleText(conversion.NameOf(m.Groups["letter"].Value)));
            var actual = localizer.Format(BuildMessage(key, conversion));
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                mismatches.Add(key + ": «" + expected + "» ≠ «" + actual + "»");
            }
        }

        handoff.Count.ShouldBe(669);
        mismatches.ShouldBeEmpty();
    }

    [GeneratedRegex(
        @"\{(?<letter>[a-z])\}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex Marker();

    private static string SampleText(string name) =>
        NumberSamples.TryGetValue(name, out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : TextSamples[name];

    private static Message BuildMessage(string key, Conversion conversion)
    {
        MessageCatalog
            .TryGet(key, out var descriptor)
            .ShouldBeTrue("handoff key '" + key + "' has no generated member");
        var arguments = descriptor.Parameters.Select(parameter =>
            conversion.Composed.TryGetValue(parameter.Name, out var nested)
                ? MessageArgument.Text(
                    parameter.Name,
                    new Message(
                        new MessageKey(nested),
                        MessageArgument.WholeNumber("count", NumberSamples[parameter.Name])
                    )
                )
                : parameter.Type switch
                {
                    MessageArgumentType.WholeNumber => MessageArgument.WholeNumber(
                        parameter.Name,
                        NumberSamples[parameter.Name]
                    ),
                    MessageArgumentType.Text => MessageArgument.Text(
                        parameter.Name,
                        TextSamples[parameter.Name]
                    ),
                    _ => throw new InvalidOperationException("No sample for " + parameter.Name),
                }
        );
        return new Message(descriptor.Key, [.. arguments]);
    }

    /// <summary>How the recipe converted one key: letter → placeholder name, and composed nested messages.</summary>
    private sealed record Conversion(
        Dictionary<string, string> Letters,
        Dictionary<string, string> Composed
    )
    {
        public string NameOf(string letter) => Letters[letter];

        public static Conversion For(JsonElement recipe, string key)
        {
            var letters = recipe
                .GetProperty("placeholders")
                .EnumerateObject()
                .ToDictionary(
                    static p => p.Name,
                    static p => p.Value.GetString()!,
                    StringComparer.Ordinal
                );
            var composed = new Dictionary<string, string>(StringComparer.Ordinal);
            if (recipe.GetProperty("keys").TryGetProperty(key, out var rule))
            {
                if (rule.TryGetProperty("placeholders", out var overrides))
                {
                    foreach (var letter in overrides.EnumerateObject())
                    {
                        letters[letter.Name] = letter.Value.GetString()!;
                    }
                }

                if (rule.TryGetProperty("arguments", out var arguments))
                {
                    foreach (var argument in arguments.EnumerateObject())
                    {
                        letters[argument.Value.GetProperty("count").GetString()!] = argument.Name;
                        composed[argument.Name] = argument
                            .Value.GetProperty("message")
                            .GetString()!;
                    }
                }
            }

            return new Conversion(letters, composed);
        }
    }
}
