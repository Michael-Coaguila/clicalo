using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Clicalo.Design.Math;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>
/// Reads <c>contrast-pairs.json</c> and measures every pair in every theme on the real composite (translucent
/// stacks over each backdrop, 8-bit colors as rendered). A pair below its minimum is CLCT002 (TEM-004, NFR-007).
/// </summary>
internal static class ContrastChecker
{
    /// <summary>Pseudo token: the tint of each category (TEM-003); the pair is checked for every category.</summary>
    public const string CategoryTint = "categoryTint";

    /// <summary>Pseudo token: the active-state wash of each category (TEM-003).</summary>
    public const string CategoryWash = "categoryWash";

    /// <summary>Separator of background layers, top first: <c>accentWash over card</c>.</summary>
    public const string Over = " over ";

    public static void Check(TokenDocument document, TokenModel model, TokenIssues issues)
    {
        var minimums = ReadMinimums(document, issues);
        var backdrops = ReadBackdrops(document, issues);
        var pairs = ReadPairs(document, model, issues);
        CheckCoverage(document, model, pairs, issues);
        if (minimums is null || backdrops is null)
        {
            return;
        }

        foreach (var theme in model.Themes)
        {
            foreach (var pair in pairs)
            {
                var minimum = pair.IsText ? minimums.Value.Key : minimums.Value.Value;
                foreach (var background in pair.Backgrounds)
                {
                    Measure(model, theme, pair, background, minimum, backdrops, issues);
                }
            }
        }
    }

    private static void Measure(
        TokenModel model,
        ThemeModel theme,
        PairDefinition pair,
        BackgroundDefinition background,
        double minimum,
        List<Rgba8> backdrops,
        TokenIssues issues
    )
    {
        var usesCategories = IsCategory(pair.Foreground) || background.Layers.Exists(IsCategory);
        IReadOnlyList<string> categories = usesCategories ? model.Categories : [string.Empty];
        var failures = new List<string>();
        var worst = double.MaxValue;
        var worstBackdrop = -1;
        foreach (var category in categories)
        {
            var layers = background.Layers.ConvertAll(layer => ColorOf(theme, layer, category));
            var measurement = ContrastEvaluator.Measure(
                ColorOf(theme, pair.Foreground, category),
                layers,
                backdrops
            );
            if (measurement.Ratio >= minimum)
            {
                continue;
            }

            failures.Add(
                usesCategories
                    ? category + " (" + Ratio(measurement.Ratio) + ")"
                    : Ratio(measurement.Ratio)
            );
            if (measurement.Ratio < worst)
            {
                worst = measurement.Ratio;
                worstBackdrop = measurement.BackdropIndex;
            }
        }

        if (failures.Count == 0)
        {
            return;
        }

        var required = minimum.ToString("0.0##", CultureInfo.InvariantCulture) + ":1";
        var message = new StringBuilder()
            .Append(pair.IsText ? "Text" : "Graphic")
            .Append(" contrast of '")
            .Append(pair.Foreground)
            .Append("' on '")
            .Append(background.Text)
            .Append("' in theme '")
            .Append(theme.Key)
            .Append(usesCategories ? "' is below " + required + " for the categories " : "' is ")
            .Append(string.Join(", ", failures))
            .Append(usesCategories ? string.Empty : ", below the required " + required)
            .Append(
                worstBackdrop >= 0
                    ? " (worst case over the backdrop " + backdrops[worstBackdrop].ToRgbHex() + ")"
                    : string.Empty
            )
            .Append(
                ". Correct the token keeping its hue and document the correction in extra-tokens.json (TEM-004)."
            )
            .ToString();
        issues.Report(TokenIds.ContrastTooLow, background.Position, message);
    }

    /// <summary>Truncated, never rounded up: 4.4996 must not print as 4.50 next to "below 4.5".</summary>
    private static string Ratio(double ratio) =>
        (System.Math.Floor(ratio * 100d) / 100d).ToString("0.00", CultureInfo.InvariantCulture)
        + ":1";

    private static bool IsCategory(string token) => token is CategoryTint or CategoryWash;

    private static Rgba8 ColorOf(ThemeModel theme, string token, string category) =>
        token switch
        {
            CategoryTint => theme.CategoryTints[category].Value,
            CategoryWash => theme.CategoryWashes[category].Value,
            _ => theme.Colors[token].Value,
        };

    private static KeyValuePair<double, double>? ReadMinimums(
        TokenDocument document,
        TokenIssues issues
    )
    {
        var minimums = JsonShape.Required(
            document,
            document.Root,
            "minimums",
            JsonKind.Object,
            issues
        );
        if (minimums is null)
        {
            return null;
        }

        JsonShape.AllowOnly(document, minimums, issues, "text", "graphic");
        var text = ReadMinimum(document, minimums, "text", Wcag.MinimumTextContrast, issues);
        var graphic = ReadMinimum(
            document,
            minimums,
            "graphic",
            Wcag.MinimumGraphicContrast,
            issues
        );
        return text is null || graphic is null
            ? null
            : new KeyValuePair<double, double>(text.Value, graphic.Value);
    }

    private static double? ReadMinimum(
        TokenDocument document,
        JsonNode owner,
        string name,
        double floor,
        TokenIssues issues
    )
    {
        var node = JsonShape.Required(document, owner, name, JsonKind.Number, issues);
        if (node is null)
        {
            return null;
        }

        if (node.NumberValue < floor || node.NumberValue > 21d)
        {
            issues.Malformed(
                document,
                node,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "the '{0}' minimum must be between {1} (WCAG 2.x AA, TEM-004) and 21.",
                    name,
                    floor
                )
            );
            return null;
        }

        return node.NumberValue;
    }

    private static List<Rgba8>? ReadBackdrops(TokenDocument document, TokenIssues issues)
    {
        var node = JsonShape.Required(document, document.Root, "backdrops", JsonKind.Array, issues);
        if (node is null)
        {
            return null;
        }

        var backdrops = new List<Rgba8>();
        foreach (var item in node.Items)
        {
            if (!JsonShape.Expect(document, item, JsonKind.String, "A backdrop", issues))
            {
                return null;
            }

            if (!CssParser.TryParseColor(item.StringValue!, out var color, out var error))
            {
                issues.Report(
                    TokenIds.InvalidColor,
                    document.At(item).Shift(1 + error.Offset),
                    "'" + item.StringValue + "' is not a valid backdrop color: " + error.Message
                );
                return null;
            }

            var value = color.ToRgba8();
            if (!value.IsOpaque)
            {
                issues.Report(
                    TokenIds.InvalidColor,
                    document.At(item),
                    "Backdrops stand for the desktop and must be opaque."
                );
                return null;
            }

            backdrops.Add(value);
        }

        if (backdrops.Count == 0)
        {
            issues.Malformed(
                document,
                node,
                "at least one backdrop is needed to measure translucent surfaces."
            );
            return null;
        }

        return backdrops;
    }

    private static List<PairDefinition> ReadPairs(
        TokenDocument document,
        TokenModel model,
        TokenIssues issues
    )
    {
        var result = new List<PairDefinition>();
        var pairs = JsonShape.Required(document, document.Root, "pairs", JsonKind.Array, issues);
        if (pairs is null)
        {
            return result;
        }

        foreach (var pair in pairs.Items)
        {
            if (!JsonShape.Expect(document, pair, JsonKind.Object, "A contrast pair", issues))
            {
                continue;
            }

            JsonShape.AllowOnly(document, pair, issues, "kind", "foreground", "backgrounds", "use");
            var kind = JsonShape.Required(document, pair, "kind", JsonKind.String, issues);
            var foreground = JsonShape.Required(
                document,
                pair,
                "foreground",
                JsonKind.String,
                issues
            );
            var backgrounds = JsonShape.Required(
                document,
                pair,
                "backgrounds",
                JsonKind.Array,
                issues
            );
            var use = JsonShape.Required(document, pair, "use", JsonKind.String, issues);
            if (kind is null || foreground is null || backgrounds is null || use is null)
            {
                continue;
            }

            if (kind.StringValue is not ("text" or "graphic"))
            {
                issues.Malformed(
                    document,
                    kind,
                    "'kind' must be \"text\" (4.5:1) or \"graphic\" (3:1)."
                );
                continue;
            }

            if (!IsKnown(model, foreground.StringValue!))
            {
                ReportUnknown(document, foreground, foreground.StringValue!, issues);
                continue;
            }

            var definition = new PairDefinition(
                string.Equals(kind.StringValue, "text", StringComparison.Ordinal),
                foreground.StringValue!
            );
            foreach (var background in backgrounds.Items)
            {
                var parsed = ReadBackground(document, model, background, issues);
                if (parsed is not null)
                {
                    definition.Backgrounds.Add(parsed);
                }
            }

            if (backgrounds.Items.Count == 0)
            {
                issues.Malformed(document, backgrounds, "a pair needs at least one background.");
            }

            result.Add(definition);
        }

        return result;
    }

    private static BackgroundDefinition? ReadBackground(
        TokenDocument document,
        TokenModel model,
        JsonNode node,
        TokenIssues issues
    )
    {
        if (!JsonShape.Expect(document, node, JsonKind.String, "A background", issues))
        {
            return null;
        }

        var text = node.StringValue!;
        var layers = new List<string>();
        foreach (var part in text.Split([Over], StringSplitOptions.None))
        {
            var layer = part.Trim();
            if (!IsKnown(model, layer))
            {
                ReportUnknown(document, node, layer, issues);
                return null;
            }

            layers.Add(layer);
        }

        return new BackgroundDefinition(text, layers, document.At(node));
    }

    private static void CheckCoverage(
        TokenDocument document,
        TokenModel model,
        List<PairDefinition> pairs,
        TokenIssues issues
    )
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            covered.Add(pair.Foreground);
            foreach (var background in pair.Backgrounds)
            {
                covered.UnionWith(background.Layers);
            }
        }

        var decorative = JsonShape.Required(
            document,
            document.Root,
            "decorative",
            JsonKind.Object,
            issues
        );
        if (decorative is null)
        {
            return;
        }

        foreach (var member in JsonShape.DataMembers(decorative))
        {
            if (!model.ColorTokens.Contains(member.Key))
            {
                ReportUnknown(document, member.Value, member.Key, issues);
            }
            else if (
                JsonShape.Expect(
                    document,
                    member.Value,
                    JsonKind.String,
                    "The reason for '" + member.Key + "'",
                    issues
                ) && string.IsNullOrWhiteSpace(member.Value.StringValue)
            )
            {
                issues.Malformed(
                    document,
                    member.Value,
                    "explain why '" + member.Key + "' needs no contrast check."
                );
            }

            covered.Add(member.Key);
        }

        foreach (var token in model.ColorTokens)
        {
            if (!covered.Contains(token))
            {
                issues.Report(
                    TokenIds.UnknownToken,
                    document.At(decorative),
                    "The token '"
                        + token
                        + "' is neither measured by a contrast pair nor declared decorative; every token needs a contrast decision (NFR-007)."
                );
            }
        }
    }

    private static bool IsKnown(TokenModel model, string token) =>
        IsCategory(token) || model.ColorTokens.Contains(token);

    private static void ReportUnknown(
        TokenDocument document,
        JsonNode node,
        string token,
        TokenIssues issues
    ) =>
        issues.Report(
            TokenIds.UnknownToken,
            document.At(node),
            "Unknown color token '" + token + "' in contrast-pairs.json."
        );

    private sealed class PairDefinition(bool isText, string foreground)
    {
        public bool IsText { get; } = isText;

        public string Foreground { get; } = foreground;

        public List<BackgroundDefinition> Backgrounds { get; } = [];
    }

    private sealed class BackgroundDefinition(
        string text,
        List<string> layers,
        DataPosition position
    )
    {
        public string Text { get; } = text;

        public List<string> Layers { get; } = layers;

        public DataPosition Position { get; } = position;
    }
}
