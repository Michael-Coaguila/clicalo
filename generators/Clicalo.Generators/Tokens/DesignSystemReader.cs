using System.Collections.Generic;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>
/// Reads <c>design-system.json</c> (direction C, D12): the spacing scale, the three control heights, the radii, the
/// icon boxes, the strokes (with the focus ring of TEM-009) and the closed type scale. Every error is a CLCT004 at
/// its position.
/// </summary>
internal static class DesignSystemReader
{
    /// <summary>Smallest text size (TEM-007).</summary>
    private const double MinimumTextSize = 11;

    private static readonly string[] Families = ["ui", "keys"];

    public static void Read(TokenDocument document, TokenModel model, TokenIssues issues)
    {
        var root = document.Root;
        JsonShape.AllowOnly(
            document,
            root,
            issues,
            "space",
            "controlHeights",
            "radii",
            "iconBoxes",
            "strokes",
            "components",
            "typography"
        );
        ReadLengths(document, root, "space", [], model.Space, issues);
        ReadLengths(
            document,
            root,
            "controlHeights",
            ["small", "medium", "large"],
            model.ControlHeights,
            issues
        );
        ReadLengths(
            document,
            root,
            "radii",
            ["small", "medium", "large", "pill"],
            model.Radii,
            issues
        );
        ReadLengths(
            document,
            root,
            "iconBoxes",
            ["badge", "small", "medium", "large"],
            model.IconBoxes,
            issues
        );
        ReadLengths(
            document,
            root,
            "strokes",
            ["line", "selected", "focusRing", "focusOffset"],
            model.Strokes,
            issues
        );
        ReadLengths(
            document,
            root,
            "components",
            [
                "switchWidth",
                "switchHeight",
                "switchKnobOff",
                "switchKnobOn",
                "sliderTrack",
                "sliderKnob",
                "scrollBar",
                "scrollFade",
                "titleBar",
                "captionButton",
            ],
            model.Components,
            issues
        );
        foreach (var stroke in model.Strokes)
        {
            if (string.Equals(stroke.Key, "focusRing", System.StringComparison.Ordinal))
            {
                model.FocusRingThickness = stroke.Value;
            }
            else if (string.Equals(stroke.Key, "focusOffset", System.StringComparison.Ordinal))
            {
                model.FocusRingOffset = stroke.Value;
            }
        }

        ReadTypography(document, root, model, issues);
    }

    private static void ReadLengths(
        TokenDocument document,
        JsonNode root,
        string section,
        string[] required,
        List<KeyValuePair<string, double>> target,
        TokenIssues issues
    )
    {
        var node = JsonShape.Required(document, root, section, JsonKind.Object, issues);
        if (node is null)
        {
            return;
        }

        foreach (var name in required)
        {
            if (node[name] is null)
            {
                issues.Malformed(document, node, "'" + section + "' must define '" + name + "'.");
            }
        }

        foreach (var member in JsonShape.DataMembers(node))
        {
            if (!JsonShape.IsIdentifier(member.Key))
            {
                issues.Malformed(
                    document,
                    member.Value,
                    "the name '" + member.Key + "' must be a camelCase identifier."
                );
                continue;
            }

            if (
                !JsonShape.Expect(
                    document,
                    member.Value,
                    JsonKind.Number,
                    "'" + section + "." + member.Key + "'",
                    issues
                )
            )
            {
                continue;
            }

            if (member.Value.NumberValue < 0d)
            {
                issues.Malformed(
                    document,
                    member.Value,
                    "'" + section + "." + member.Key + "' cannot be negative."
                );
                continue;
            }

            target.Add(new KeyValuePair<string, double>(member.Key, member.Value.NumberValue));
        }

        if (target.Count == 0)
        {
            issues.Malformed(document, node, "'" + section + "' must define at least one value.");
        }
    }

    private static void ReadTypography(
        TokenDocument document,
        JsonNode root,
        TokenModel model,
        TokenIssues issues
    )
    {
        var node = JsonShape.Required(document, root, "typography", JsonKind.Object, issues);
        if (node is null)
        {
            return;
        }

        foreach (var member in JsonShape.DataMembers(node))
        {
            if (!JsonShape.IsIdentifier(member.Key))
            {
                issues.Malformed(
                    document,
                    member.Value,
                    "the text style '" + member.Key + "' must be a camelCase identifier."
                );
                continue;
            }

            if (
                !JsonShape.Expect(
                    document,
                    member.Value,
                    JsonKind.Object,
                    "The text style '" + member.Key + "'",
                    issues
                )
            )
            {
                continue;
            }

            var style = ReadStyle(document, member.Key, member.Value, issues);
            if (style is not null)
            {
                model.TextStyles.Add(style);
            }
        }

        if (model.TextStyles.Count == 0)
        {
            issues.Malformed(document, node, "'typography' must define at least one text style.");
        }
    }

    private static TextStyleModel? ReadStyle(
        TokenDocument document,
        string key,
        JsonNode node,
        TokenIssues issues
    )
    {
        JsonShape.AllowOnly(
            document,
            node,
            issues,
            "family",
            "weight",
            "size",
            "lineHeight",
            "letterSpacing",
            "uppercase"
        );
        var family = JsonShape.Required(document, node, "family", JsonKind.String, issues);
        var weight = JsonShape.Required(document, node, "weight", JsonKind.Number, issues);
        var size = JsonShape.Required(document, node, "size", JsonKind.Number, issues);
        var line = JsonShape.Required(document, node, "lineHeight", JsonKind.Number, issues);
        if (family is null || weight is null || size is null || line is null)
        {
            return null;
        }

        var ok = true;
        if (System.Array.IndexOf(Families, family.StringValue) < 0)
        {
            issues.Malformed(
                document,
                family,
                "the family of '" + key + "' must be 'ui' or 'keys'."
            );
            ok = false;
        }

        if (!weight.TryGetInt64(out var w) || w is not (400 or 500 or 700))
        {
            issues.Malformed(
                document,
                weight,
                "the weight of '" + key + "' must be 400, 500 or 700 (the bundled faces)."
            );
            ok = false;
        }

        if (size.NumberValue < MinimumTextSize)
        {
            issues.Malformed(
                document,
                size,
                "the size of '" + key + "' cannot be below 11 (TEM-007)."
            );
            ok = false;
        }

        if (line.NumberValue < size.NumberValue)
        {
            issues.Malformed(
                document,
                line,
                "the line height of '" + key + "' cannot be smaller than its size."
            );
            ok = false;
        }

        var spacing = 0d;
        if (node["letterSpacing"] is { } letter)
        {
            if (
                JsonShape.Expect(document, letter, JsonKind.Number, "'letterSpacing'", issues)
                && letter.NumberValue is >= 0d and <= 0.2
            )
            {
                spacing = letter.NumberValue;
            }
            else
            {
                issues.Malformed(
                    document,
                    letter,
                    "the letter spacing of '" + key + "' must be from 0 to 0.2 em."
                );
                ok = false;
            }
        }

        var uppercase = false;
        if (node["uppercase"] is { } upper)
        {
            if (JsonShape.Expect(document, upper, JsonKind.Boolean, "'uppercase'", issues))
            {
                uppercase = upper.BooleanValue;
            }
            else
            {
                ok = false;
            }
        }

        return ok
            ? new TextStyleModel(
                key,
                family.StringValue!,
                (int)w,
                size.NumberValue,
                line.NumberValue,
                spacing,
                uppercase
            )
            : null;
    }
}
