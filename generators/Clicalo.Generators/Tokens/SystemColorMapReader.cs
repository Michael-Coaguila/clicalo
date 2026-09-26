using System;
using System.Collections.Generic;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>
/// Reads <c>hc-system-map.json</c>: which Windows contrast-theme color replaces each token when Windows runs a
/// contrast theme (TEM-001). Every token must be mapped, and only real <c>SystemColors</c> properties are accepted.
/// </summary>
internal static class SystemColorMapReader
{
    /// <summary>The <c>System.Windows.SystemColors</c> color properties (WPF, .NET 10).</summary>
    private static readonly HashSet<string> WpfSystemColors = new(StringComparer.Ordinal)
    {
        "ActiveBorderColor",
        "ActiveCaptionColor",
        "ActiveCaptionTextColor",
        "AppWorkspaceColor",
        "ControlColor",
        "ControlDarkColor",
        "ControlDarkDarkColor",
        "ControlLightColor",
        "ControlLightLightColor",
        "ControlTextColor",
        "DesktopColor",
        "GradientActiveCaptionColor",
        "GradientInactiveCaptionColor",
        "GrayTextColor",
        "HighlightColor",
        "HighlightTextColor",
        "HotTrackColor",
        "InactiveBorderColor",
        "InactiveCaptionColor",
        "InactiveCaptionTextColor",
        "InfoColor",
        "InfoTextColor",
        "MenuBarColor",
        "MenuColor",
        "MenuHighlightColor",
        "MenuTextColor",
        "ScrollBarColor",
        "WindowColor",
        "WindowFrameColor",
        "WindowTextColor",
    };

    public static void Read(TokenDocument document, TokenModel model, TokenIssues issues)
    {
        var wpfBySystemKey = ReadSystemColors(document, issues);
        var tokens = JsonShape.Required(document, document.Root, "tokens", JsonKind.Object, issues);
        if (tokens is not null)
        {
            foreach (var member in JsonShape.DataMembers(tokens))
            {
                if (!model.ColorTokens.Contains(member.Key))
                {
                    issues.Report(
                        TokenIds.UnknownToken,
                        document.At(member.Value),
                        "hc-system-map.json maps '" + member.Key + "', which is not a color token."
                    );
                    continue;
                }

                var wpf = Lookup(document, member.Value, wpfBySystemKey, issues);
                if (wpf is not null)
                {
                    model.SystemColorByToken[member.Key] = wpf;
                }
            }

            foreach (var token in model.ColorTokens)
            {
                if (tokens[token] is null)
                {
                    issues.Report(
                        TokenIds.UnknownToken,
                        document.At(tokens),
                        "hc-system-map.json does not map the token '"
                            + token
                            + "'; every token needs a system color."
                    );
                }
            }
        }

        var categories = JsonShape.Required(
            document,
            document.Root,
            "categories",
            JsonKind.Object,
            issues
        );
        if (categories is not null)
        {
            var tint = JsonShape.Required(document, categories, "tint", JsonKind.String, issues);
            var wash = JsonShape.Required(document, categories, "wash", JsonKind.String, issues);
            model.SystemCategoryTint =
                (tint is null ? null : Lookup(document, tint, wpfBySystemKey, issues))
                ?? string.Empty;
            model.SystemCategoryWash =
                (wash is null ? null : Lookup(document, wash, wpfBySystemKey, issues))
                ?? string.Empty;
        }

        CheckGuaranteedPairs(document, wpfBySystemKey, issues);
    }

    private static Dictionary<string, string> ReadSystemColors(
        TokenDocument document,
        TokenIssues issues
    )
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var systemColors = JsonShape.Required(
            document,
            document.Root,
            "systemColors",
            JsonKind.Object,
            issues
        );
        if (systemColors is null)
        {
            return result;
        }

        foreach (var member in JsonShape.DataMembers(systemColors))
        {
            if (
                !JsonShape.Expect(
                    document,
                    member.Value,
                    JsonKind.Object,
                    "The system color '" + member.Key + "'",
                    issues
                )
            )
            {
                continue;
            }

            JsonShape.Required(document, member.Value, "win32", JsonKind.String, issues);
            JsonShape.Required(document, member.Value, "role", JsonKind.String, issues);
            var wpf = JsonShape.Required(document, member.Value, "wpf", JsonKind.String, issues);
            if (wpf is null)
            {
                continue;
            }

            if (!WpfSystemColors.Contains(wpf.StringValue!))
            {
                issues.Report(
                    TokenIds.UnknownToken,
                    document.At(wpf),
                    "'" + wpf.StringValue + "' is not a System.Windows.SystemColors color property."
                );
                continue;
            }

            result[member.Key] = wpf.StringValue!;
        }

        return result;
    }

    private static void CheckGuaranteedPairs(
        TokenDocument document,
        Dictionary<string, string> wpfBySystemKey,
        TokenIssues issues
    )
    {
        var pairs = JsonShape.Required(
            document,
            document.Root,
            "guaranteedPairs",
            JsonKind.Array,
            issues
        );
        if (pairs is null)
        {
            return;
        }

        foreach (var pair in pairs.Items)
        {
            if (!JsonShape.Expect(document, pair, JsonKind.Object, "A guaranteed pair", issues))
            {
                continue;
            }

            JsonShape.Required(document, pair, "basis", JsonKind.String, issues);
            foreach (var side in new[] { "foreground", "background" })
            {
                var node = JsonShape.Required(document, pair, side, JsonKind.String, issues);
                if (node is not null)
                {
                    Lookup(document, node, wpfBySystemKey, issues);
                }
            }
        }
    }

    private static string? Lookup(
        TokenDocument document,
        JsonNode node,
        Dictionary<string, string> wpfBySystemKey,
        TokenIssues issues
    )
    {
        if (!JsonShape.Expect(document, node, JsonKind.String, "A system color reference", issues))
        {
            return null;
        }

        if (wpfBySystemKey.TryGetValue(node.StringValue!, out var wpf))
        {
            return wpf;
        }

        issues.Report(
            TokenIds.UnknownToken,
            document.At(node),
            "'" + node.StringValue + "' is not declared in 'systemColors' of hc-system-map.json."
        );
        return null;
    }
}
