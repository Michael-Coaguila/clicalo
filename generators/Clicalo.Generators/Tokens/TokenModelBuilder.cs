using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Clicalo.Design.Math;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>
/// Reads <c>data/tokens</c> into a <see cref="TokenModel"/>: palettes, extra tokens, documented corrections,
/// aliases, category colors, shapes, motion and the Windows high-contrast map. Every data error becomes a
/// <see cref="TokenIssue"/> at its exact position; the contrast pairs are then checked by <see cref="ContrastChecker"/>.
/// </summary>
internal sealed class TokenModelBuilder
{
    private const string HighContrastFlag = "hc";
    private const string TintLightness = "tintL";
    private const string WashAlpha = "washA";
    private const string Border = "border";

    private readonly TokenIssues _issues = new();
    private readonly Dictionary<string, TokenDocument> _documents = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly List<ThemeDraft> _drafts = [];
    private readonly TokenModel _model = new();
    private double _maxDeltaEok = GamutMapping.JustNoticeableDifference;

    private TokenModelBuilder() { }

    public static TokenModel Build(IReadOnlyList<TokenSourceFile> files)
    {
        var builder = new TokenModelBuilder();
        builder.Run(files);
        builder._model.Issues.AddRange(builder._issues.Items);
        return builder._model;
    }

    private TokenDocument Extra => _documents[TokenFiles.ExtraTokens];

    private void Run(IReadOnlyList<TokenSourceFile> files)
    {
        if (!Load(files))
        {
            return;
        }

        ReadGamutLimit();
        var names = ReadThemeNames();
        ReadPalettes(names);
        ReadExtraColors();
        ApplyCorrections();
        ResolveColors();
        CheckTokenSets();
        ReadCategories();
        ReadShapes();
        _model.Motion.AddRange(MotionReader.Read(_documents[TokenFiles.Motion], _issues));
        SystemColorMapReader.Read(_documents[TokenFiles.HighContrastSystemMap], _model, _issues);
        if (HasStructuralIssue())
        {
            // Contrast is only meaningful on a complete, valid palette.
            return;
        }

        foreach (var draft in _drafts)
        {
            _model.Themes.Add(draft.Theme);
        }

        ContrastChecker.Check(_documents[TokenFiles.ContrastPairs], _model, _issues);
        _model.CanEmit = !HasStructuralIssue();
    }

    private bool HasStructuralIssue()
    {
        foreach (var issue in _issues.Items)
        {
            if (issue.Id is not (TokenIds.ContrastTooLow or TokenIds.OutOfGamut))
            {
                return true;
            }
        }

        return false;
    }

    private bool Load(IReadOnlyList<TokenSourceFile> files)
    {
        var ok = true;
        foreach (var name in TokenFiles.All)
        {
            TokenSourceFile? file = null;
            foreach (var candidate in files)
            {
                if (
                    string.Equals(
                        Path.GetFileName(candidate.Path),
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    file = candidate;
                    break;
                }
            }

            if (file is null)
            {
                _issues.Report(
                    TokenIds.MissingFile,
                    null,
                    TokenFiles.Directory
                        + "/"
                        + name
                        + " is not an AdditionalFiles item of this project; the design tokens cannot be generated."
                );
                ok = false;
                continue;
            }

            if (file.Text is null)
            {
                _issues.Report(
                    TokenIds.MalformedFile,
                    new DataPosition(file.Path, 1, 1),
                    name + ": the file cannot be read."
                );
                ok = false;
                continue;
            }

            try
            {
                var root = MiniJson.Parse(file.Text);
                var document = new TokenDocument(file.Path, name, root);
                if (JsonShape.Expect(document, root, JsonKind.Object, "The root", _issues))
                {
                    _documents[name] = document;
                }
                else
                {
                    ok = false;
                }
            }
            catch (JsonParseException ex)
            {
                _issues.Report(
                    TokenIds.MalformedFile,
                    new DataPosition(file.Path, ex.Line, ex.Column),
                    name + ": " + ex.Message
                );
                ok = false;
            }
        }

        return ok;
    }

    private void ReadGamutLimit()
    {
        var gamut = JsonShape.Required(Extra, Extra.Root, "gamutMapping", JsonKind.Object, _issues);
        var max = gamut is null
            ? null
            : JsonShape.Required(Extra, gamut, "maxDeltaEOK", JsonKind.Number, _issues);
        if (max is null)
        {
            return;
        }

        if (max.NumberValue <= 0d || max.NumberValue > 1d)
        {
            _issues.Malformed(Extra, max, "'maxDeltaEOK' must be greater than 0 and at most 1.");
            return;
        }

        _maxDeltaEok = max.NumberValue;
    }

    private Dictionary<string, KeyValuePair<string, JsonNode>> ReadThemeNames()
    {
        var names = new Dictionary<string, KeyValuePair<string, JsonNode>>(StringComparer.Ordinal);
        var themes = JsonShape.Required(Extra, Extra.Root, "themes", JsonKind.Object, _issues);
        if (themes is null)
        {
            return names;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in JsonShape.DataMembers(themes))
        {
            if (
                !JsonShape.Expect(
                    Extra,
                    member.Value,
                    JsonKind.String,
                    "The name of theme '" + member.Key + "'",
                    _issues
                )
            )
            {
                continue;
            }

            var name = member.Value.StringValue!;
            if (!JsonShape.IsPascalIdentifier(name))
            {
                _issues.Malformed(
                    Extra,
                    member.Value,
                    "the theme name '" + name + "' must be a PascalCase C# identifier."
                );
            }
            else if (!used.Add(name))
            {
                _issues.Report(
                    TokenIds.UnknownToken,
                    Extra.At(member.Value),
                    "The theme name '" + name + "' is used twice."
                );
            }
            else
            {
                names[member.Key] = new KeyValuePair<string, JsonNode>(name, member.Value);
            }
        }

        return names;
    }

    private void ReadPalettes(Dictionary<string, KeyValuePair<string, JsonNode>> names)
    {
        var palettes = _documents[TokenFiles.ThemePalettes];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in JsonShape.DataMembers(palettes.Root))
        {
            if (
                !JsonShape.Expect(
                    palettes,
                    member.Value,
                    JsonKind.Object,
                    "The theme '" + member.Key + "'",
                    _issues
                )
            )
            {
                continue;
            }

            if (!names.TryGetValue(member.Key, out var name))
            {
                _issues.Report(
                    TokenIds.UnknownToken,
                    palettes.At(member.Value),
                    "The theme '" + member.Key + "' has no C# name in extra-tokens.json 'themes'."
                );
                continue;
            }

            seen.Add(member.Key);
            var isHighContrast =
                member.Value[HighContrastFlag] is { Kind: JsonKind.Boolean, BooleanValue: true };
            var draft = new ThemeDraft(
                new ThemeModel(member.Key, name.Key, isHighContrast, palettes.At(member.Value))
            );
            ReadPaletteTheme(palettes, member.Value, draft);
            _drafts.Add(draft);
        }

        foreach (var name in names)
        {
            if (!seen.Contains(name.Key))
            {
                _issues.Report(
                    TokenIds.UnknownToken,
                    Extra.At(name.Value.Value),
                    "The theme '"
                        + name.Key
                        + "' is named in extra-tokens.json but missing from theme-palettes.json."
                );
            }
        }
    }

    private void ReadPaletteTheme(TokenDocument palettes, JsonNode theme, ThemeDraft draft)
    {
        foreach (var member in JsonShape.DataMembers(theme))
        {
            var node = member.Value;
            switch (member.Key)
            {
                case HighContrastFlag:
                    JsonShape.Expect(palettes, node, JsonKind.Boolean, "'hc'", _issues);
                    break;
                case TintLightness:
                case WashAlpha:
                    if (
                        JsonShape.Expect(
                            palettes,
                            node,
                            JsonKind.Number,
                            "'" + member.Key + "'",
                            _issues
                        )
                    )
                    {
                        draft.Numbers[member.Key] = new NumberEntry(
                            node.NumberValue,
                            palettes.At(node),
                            null
                        );
                    }

                    break;
                default:
                    if (!JsonShape.IsIdentifier(member.Key))
                    {
                        _issues.Malformed(
                            palettes,
                            node,
                            "the token name '" + member.Key + "' must be a camelCase identifier."
                        );
                    }
                    else if (
                        JsonShape.Expect(
                            palettes,
                            node,
                            JsonKind.String,
                            "The token '" + member.Key + "'",
                            _issues
                        )
                    )
                    {
                        draft.Add(
                            new TokenEntry(member.Key, node.StringValue!, palettes.At(node), null)
                            {
                                IsBorderShorthand = string.Equals(
                                    member.Key,
                                    Border,
                                    StringComparison.Ordinal
                                ),
                            }
                        );
                    }

                    break;
            }
        }
    }

    private void ReadExtraColors()
    {
        var colors = JsonShape.Required(Extra, Extra.Root, "colors", JsonKind.Object, _issues);
        if (colors is null)
        {
            return;
        }

        foreach (var theme in JsonShape.DataMembers(colors))
        {
            var draft = FindDraft(Extra, theme.Key, theme.Value);
            if (
                draft is null
                || !JsonShape.Expect(
                    Extra,
                    theme.Value,
                    JsonKind.Object,
                    "The theme '" + theme.Key + "'",
                    _issues
                )
            )
            {
                continue;
            }

            foreach (var token in JsonShape.DataMembers(theme.Value))
            {
                if (!JsonShape.IsIdentifier(token.Key))
                {
                    _issues.Malformed(
                        Extra,
                        token.Value,
                        "the token name '" + token.Key + "' must be a camelCase identifier."
                    );
                }
                else if (
                    draft.Entries.ContainsKey(token.Key) || token.Key is TintLightness or WashAlpha
                )
                {
                    _issues.Report(
                        TokenIds.UnknownToken,
                        Extra.At(token.Value),
                        "The token '"
                            + token.Key
                            + "' of theme '"
                            + theme.Key
                            + "' is already defined in theme-palettes.json."
                    );
                }
                else if (
                    JsonShape.Expect(
                        Extra,
                        token.Value,
                        JsonKind.String,
                        "The token '" + token.Key + "'",
                        _issues
                    )
                )
                {
                    draft.Add(
                        new TokenEntry(
                            token.Key,
                            token.Value.StringValue!,
                            Extra.At(token.Value),
                            null
                        )
                    );
                }
            }
        }
    }

    private void ApplyCorrections()
    {
        var corrections = JsonShape.Required(
            Extra,
            Extra.Root,
            "corrections",
            JsonKind.Object,
            _issues
        );
        if (corrections is null)
        {
            return;
        }

        foreach (var theme in JsonShape.DataMembers(corrections))
        {
            var draft = FindDraft(Extra, theme.Key, theme.Value);
            if (
                draft is null
                || !JsonShape.Expect(
                    Extra,
                    theme.Value,
                    JsonKind.Object,
                    "The theme '" + theme.Key + "'",
                    _issues
                )
            )
            {
                continue;
            }

            foreach (var correction in JsonShape.DataMembers(theme.Value))
            {
                if (
                    JsonShape.Expect(
                        Extra,
                        correction.Value,
                        JsonKind.Object,
                        "The correction '" + correction.Key + "'",
                        _issues
                    )
                )
                {
                    ApplyCorrection(draft, correction.Key, correction.Value);
                }
            }
        }
    }

    private void ApplyCorrection(ThemeDraft draft, string key, JsonNode correction)
    {
        var requirement = JsonShape.Required(
            Extra,
            correction,
            "requirement",
            JsonKind.String,
            _issues
        );
        var reason = JsonShape.Required(Extra, correction, "reason", JsonKind.String, _issues);
        if (reason is not null && string.IsNullOrWhiteSpace(reason.StringValue))
        {
            _issues.Malformed(Extra, reason, "every correction must explain its reason.");
        }

        var numeric = key is TintLightness or WashAlpha;
        var kind = numeric ? JsonKind.Number : JsonKind.String;
        var from = JsonShape.Required(Extra, correction, "from", kind, _issues);
        var to = JsonShape.Required(Extra, correction, "to", kind, _issues);
        if (requirement is null || from is null || to is null)
        {
            return;
        }

        var note = requirement.StringValue + ": corrected from ";
        if (numeric)
        {
            if (!draft.Numbers.TryGetValue(key, out var number))
            {
                ReportUnknownCorrection(draft, key, correction);
            }
            else if (!number.Value.Equals(from.NumberValue))
            {
                ReportStale(
                    draft,
                    key,
                    from.NumberText!,
                    number.Value.ToString("R", CultureInfo.InvariantCulture),
                    from
                );
            }
            else
            {
                draft.Numbers[key] = new NumberEntry(
                    to.NumberValue,
                    Extra.At(to),
                    note + number.Value.ToString("R", CultureInfo.InvariantCulture)
                );
            }

            return;
        }

        if (!draft.Entries.TryGetValue(key, out var entry))
        {
            ReportUnknownCorrection(draft, key, correction);
        }
        else if (!string.Equals(entry.Text, from.StringValue, StringComparison.Ordinal))
        {
            ReportStale(draft, key, from.StringValue!, entry.Text, from);
        }
        else if (JsonShape.IsIdentifier(to.StringValue!))
        {
            _issues.Report(
                TokenIds.InvalidColor,
                Extra.At(to).Shift(1),
                "A correction must give a color value, not an alias."
            );
        }
        else
        {
            entry.Note = note + entry.Text;
            entry.Text = to.StringValue!;
            entry.Position = Extra.At(to);
        }
    }

    private void ReportUnknownCorrection(ThemeDraft draft, string key, JsonNode correction) =>
        _issues.Report(
            TokenIds.UnknownToken,
            Extra.At(correction),
            "The correction targets '"
                + key
                + "', which theme '"
                + draft.Theme.Key
                + "' does not define."
        );

    private void ReportStale(
        ThemeDraft draft,
        string key,
        string expected,
        string current,
        JsonNode from
    ) =>
        _issues.Report(
            TokenIds.StaleCorrection,
            Extra.At(from),
            "The correction of '"
                + key
                + "' in theme '"
                + draft.Theme.Key
                + "' starts from '"
                + expected
                + "' but the value is now '"
                + current
                + "'; re-check the contrast and update or remove the correction."
        );

    private ThemeDraft? FindDraft(TokenDocument document, string key, JsonNode node)
    {
        foreach (var draft in _drafts)
        {
            if (string.Equals(draft.Theme.Key, key, StringComparison.Ordinal))
            {
                return draft;
            }
        }

        _issues.Report(TokenIds.UnknownToken, document.At(node), "Unknown theme '" + key + "'.");
        return null;
    }

    private void ResolveColors()
    {
        foreach (var draft in _drafts)
        {
            foreach (var entry in draft.Order)
            {
                var resolved = Resolve(draft, entry, new HashSet<string>(StringComparer.Ordinal));
                if (resolved is not null)
                {
                    draft.Theme.Colors[entry.Key] = resolved;
                }
            }
        }
    }

    private ResolvedColor? Resolve(ThemeDraft draft, TokenEntry entry, HashSet<string> visiting)
    {
        if (draft.Theme.Colors.TryGetValue(entry.Key, out var done))
        {
            return done;
        }

        if (!entry.IsAlias)
        {
            return ParseColor(draft, entry);
        }

        if (!visiting.Add(entry.Key))
        {
            _issues.Report(
                TokenIds.UnknownToken,
                entry.Position,
                "The alias '" + entry.Key + "' is part of a cycle."
            );
            return null;
        }

        if (!draft.Entries.TryGetValue(entry.Text, out var target))
        {
            _issues.Report(
                TokenIds.UnknownToken,
                entry.Position,
                "The token '"
                    + entry.Key
                    + "' refers to '"
                    + entry.Text
                    + "', which theme '"
                    + draft.Theme.Key
                    + "' does not define."
            );
            return null;
        }

        var value = Resolve(draft, target, visiting);
        return value is null
            ? null
            : new ResolvedColor(
                value.Value,
                value.Source,
                entry.Position,
                "Same as " + entry.Text + "."
            );
    }

    private ResolvedColor? ParseColor(ThemeDraft draft, TokenEntry entry)
    {
        CssColor color;
        CssSyntaxError error;
        var ok = entry.IsBorderShorthand
            ? ParseBorder(draft, entry, out color, out error)
            : CssParser.TryParseColor(entry.Text, out color, out error);
        if (!ok)
        {
            // The node column points at the opening quote of the JSON string.
            _issues.Report(
                TokenIds.InvalidColor,
                entry.Position.Shift(1 + error.Offset),
                "'"
                    + entry.Text
                    + "' is not a valid color for '"
                    + entry.Key
                    + "': "
                    + error.Message
            );
            return null;
        }

        var mapped = color.MapToSrgb();
        if (!mapped.WasInGamut && mapped.DeltaEok > _maxDeltaEok)
        {
            ReportGamut(entry.Key, draft.Theme.Key, entry.Text, mapped.DeltaEok, entry.Position);
        }

        return new ResolvedColor(color.ToRgba8(), entry.Text, entry.Position, entry.Note);
    }

    private static bool ParseBorder(
        ThemeDraft draft,
        TokenEntry entry,
        out CssColor color,
        out CssSyntaxError error
    )
    {
        color = default;
        if (!CssParser.TryParseBorder(entry.Text, out var border, out error))
        {
            return false;
        }

        draft.Theme.BorderThickness = border.Width;
        color = border.Color;
        return true;
    }

    private void ReportGamut(
        string key,
        string theme,
        string value,
        double deltaEok,
        DataPosition position
    ) =>
        _issues.Report(
            TokenIds.OutOfGamut,
            position,
            string.Format(
                CultureInfo.InvariantCulture,
                "'{0}' in theme '{1}' ({2}) is outside sRGB and gamut mapping moves it by ΔEOK {3:0.0000}, above the limit {4}; lower its chroma.",
                key,
                theme,
                value,
                deltaEok,
                _maxDeltaEok
            )
        );

    private void CheckTokenSets()
    {
        foreach (var draft in _drafts)
        {
            foreach (var key in draft.Order)
            {
                if (!_model.ColorTokens.Contains(key.Key))
                {
                    _model.ColorTokens.Add(key.Key);
                }
            }
        }

        foreach (var draft in _drafts)
        {
            foreach (var key in _model.ColorTokens)
            {
                if (!draft.Entries.ContainsKey(key))
                {
                    _issues.Report(
                        TokenIds.UnknownToken,
                        draft.Theme.Position,
                        "Theme '"
                            + draft.Theme.Key
                            + "' does not define the token '"
                            + key
                            + "'; every theme needs every token."
                    );
                }
            }

            if (!draft.Theme.IsHighContrast)
            {
                foreach (var number in new[] { TintLightness, WashAlpha })
                {
                    if (!draft.Numbers.ContainsKey(number))
                    {
                        _issues.Report(
                            TokenIds.UnknownToken,
                            draft.Theme.Position,
                            "Theme '"
                                + draft.Theme.Key
                                + "' needs '"
                                + number
                                + "' for the category colors (TEM-003)."
                        );
                    }
                }
            }
        }
    }

    private void ReadCategories()
    {
        var categories = JsonShape.Required(
            Extra,
            Extra.Root,
            "categories",
            JsonKind.Object,
            _issues
        );
        if (categories is null)
        {
            return;
        }

        var hues = JsonShape.Required(Extra, categories, "hues", JsonKind.Object, _issues);
        var tintChroma = JsonShape.Required(
            Extra,
            categories,
            "tintChroma",
            JsonKind.Number,
            _issues
        );
        var washLightness = JsonShape.Required(
            Extra,
            categories,
            "washLightness",
            JsonKind.Number,
            _issues
        );
        var washChroma = JsonShape.Required(
            Extra,
            categories,
            "washChroma",
            JsonKind.Number,
            _issues
        );
        var highContrast = JsonShape.Required(
            Extra,
            categories,
            "highContrast",
            JsonKind.Object,
            _issues
        );
        if (
            hues is null
            || tintChroma is null
            || washLightness is null
            || washChroma is null
            || highContrast is null
        )
        {
            return;
        }

        var hcTint = JsonShape.Required(Extra, highContrast, "tint", JsonKind.String, _issues);
        var hcWash = JsonShape.Required(Extra, highContrast, "wash", JsonKind.String, _issues);
        foreach (var hue in JsonShape.DataMembers(hues))
        {
            if (!JsonShape.IsIdentifier(hue.Key))
            {
                _issues.Malformed(
                    Extra,
                    hue.Value,
                    "the category '" + hue.Key + "' must be a camelCase identifier."
                );
                continue;
            }

            if (
                !JsonShape.Expect(
                    Extra,
                    hue.Value,
                    JsonKind.Number,
                    "The hue of '" + hue.Key + "'",
                    _issues
                )
            )
            {
                continue;
            }

            if (hue.Value.NumberValue is < 0d or > 360d)
            {
                _issues.Report(
                    TokenIds.InvalidColor,
                    Extra.At(hue.Value),
                    "The hue of '" + hue.Key + "' must be between 0 and 360."
                );
                continue;
            }

            _model.Categories.Add(hue.Key);
            foreach (var draft in _drafts)
            {
                if (draft.Theme.IsHighContrast)
                {
                    AddFixedCategoryColor(draft, hue.Key, hcTint, draft.Theme.CategoryTints);
                    AddFixedCategoryColor(draft, hue.Key, hcWash, draft.Theme.CategoryWashes);
                }
                else if (
                    draft.Numbers.TryGetValue(TintLightness, out var tintL)
                    && draft.Numbers.TryGetValue(WashAlpha, out var washA)
                )
                {
                    AddDerivedCategoryColor(
                        draft,
                        hue,
                        draft.Theme.CategoryTints,
                        tintL.Value,
                        tintChroma.NumberValue,
                        1d,
                        tintL.Note
                    );
                    AddDerivedCategoryColor(
                        draft,
                        hue,
                        draft.Theme.CategoryWashes,
                        washLightness.NumberValue,
                        washChroma.NumberValue,
                        washA.Value,
                        washA.Note
                    );
                }
            }
        }

        if (_model.Categories.Count == 0)
        {
            _issues.Malformed(Extra, hues, "'hues' must define at least one category (TEM-003).");
        }
    }

    private void AddFixedCategoryColor(
        ThemeDraft draft,
        string category,
        JsonNode? node,
        Dictionary<string, ResolvedColor> target
    )
    {
        if (node is null)
        {
            return;
        }

        var entry = new TokenEntry(category, node.StringValue!, Extra.At(node), null);
        var color = ParseColor(draft, entry);
        if (color is not null)
        {
            target[category] = color;
        }
    }

    private void AddDerivedCategoryColor(
        ThemeDraft draft,
        KeyValuePair<string, JsonNode> hue,
        Dictionary<string, ResolvedColor> target,
        double lightness,
        double chroma,
        double alpha,
        string? note
    )
    {
        if (lightness is < 0d or > 1d || chroma < 0d || alpha is < 0d or > 1d)
        {
            _issues.Report(
                TokenIds.InvalidColor,
                Extra.At(hue.Value),
                "The category colors of '"
                    + hue.Key
                    + "' in theme '"
                    + draft.Theme.Key
                    + "' have components out of range."
            );
            return;
        }

        var oklch = new Oklch(lightness, chroma, hue.Value.NumberValue, alpha);
        var mapped = GamutMapping.ToSrgb(oklch);
        var source = oklch.ToString();
        if (!mapped.WasInGamut && mapped.DeltaEok > _maxDeltaEok)
        {
            ReportGamut(
                "category " + hue.Key,
                draft.Theme.Key,
                source,
                mapped.DeltaEok,
                Extra.At(hue.Value)
            );
        }

        target[hue.Key] = new ResolvedColor(
            mapped.Color.ToRgba8(),
            source,
            Extra.At(hue.Value),
            note
        );
    }

    private void ReadShapes()
    {
        var radii = JsonShape.Required(Extra, Extra.Root, "radii", JsonKind.Object, _issues);
        if (radii is not null)
        {
            foreach (var radius in JsonShape.DataMembers(radii))
            {
                if (ReadLength(radius.Key, radius.Value, "radius"))
                {
                    _model.Radii.Add(
                        new KeyValuePair<string, double>(radius.Key, radius.Value.NumberValue)
                    );
                }
            }
        }

        var shadows = JsonShape.Required(Extra, Extra.Root, "shadows", JsonKind.Object, _issues);
        if (shadows is not null)
        {
            foreach (var shadow in JsonShape.DataMembers(shadows))
            {
                ReadShadow(shadow.Key, shadow.Value);
            }
        }

        var ring = JsonShape.Required(Extra, Extra.Root, "focusRing", JsonKind.Object, _issues);
        var thickness = ring is null
            ? null
            : JsonShape.Required(Extra, ring, "thickness", JsonKind.Number, _issues);
        var offset = ring is null
            ? null
            : JsonShape.Required(Extra, ring, "offset", JsonKind.Number, _issues);
        if (
            thickness is not null
            && offset is not null
            && ReadLength("thickness", thickness, "focus ring thickness")
            && ReadLength("offset", offset, "focus ring offset")
        )
        {
            _model.FocusRingThickness = thickness.NumberValue;
            _model.FocusRingOffset = offset.NumberValue;
        }
    }

    private void ReadShadow(string key, JsonNode shadow)
    {
        if (!JsonShape.IsIdentifier(key))
        {
            _issues.Malformed(
                Extra,
                shadow,
                "the shadow '" + key + "' must be a camelCase identifier."
            );
            return;
        }

        if (!JsonShape.Expect(Extra, shadow, JsonKind.Object, "The shadow '" + key + "'", _issues))
        {
            return;
        }

        var x = JsonShape.Required(Extra, shadow, "offsetX", JsonKind.Number, _issues);
        var y = JsonShape.Required(Extra, shadow, "offsetY", JsonKind.Number, _issues);
        var blur = JsonShape.Required(Extra, shadow, "blur", JsonKind.Number, _issues);
        var opacity = JsonShape.Required(Extra, shadow, "opacity", JsonKind.Number, _issues);
        if (
            x is null
            || y is null
            || blur is null
            || opacity is null
            || !ReadLength("blur", blur, "shadow blur")
        )
        {
            return;
        }

        if (opacity.NumberValue is < 0d or > 1d)
        {
            _issues.Malformed(Extra, opacity, "the shadow opacity must be between 0 and 1.");
            return;
        }

        _model.Shadows.Add(
            new ShadowModel(
                key,
                x.NumberValue,
                y.NumberValue,
                blur.NumberValue,
                opacity.NumberValue
            )
        );
    }

    private bool ReadLength(string key, JsonNode node, string what)
    {
        if (!JsonShape.IsIdentifier(key))
        {
            _issues.Malformed(
                Extra,
                node,
                "the " + what + " '" + key + "' must be a camelCase identifier."
            );
            return false;
        }

        if (
            !JsonShape.Expect(
                Extra,
                node,
                JsonKind.Number,
                "The " + what + " '" + key + "'",
                _issues
            )
        )
        {
            return false;
        }

        if (node.NumberValue < 0d)
        {
            _issues.Malformed(Extra, node, "the " + what + " '" + key + "' cannot be negative.");
            return false;
        }

        return true;
    }

    /// <summary>A theme being read: its declared entries in order, plus the numeric members.</summary>
    private sealed class ThemeDraft(ThemeModel theme)
    {
        public ThemeModel Theme { get; } = theme;

        public List<TokenEntry> Order { get; } = [];

        public Dictionary<string, TokenEntry> Entries { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, NumberEntry> Numbers { get; } = new(StringComparer.Ordinal);

        public void Add(TokenEntry entry)
        {
            Order.Add(entry);
            Entries[entry.Key] = entry;
        }
    }

    /// <summary>A numeric theme member (<c>tintL</c>, <c>washA</c>) after corrections.</summary>
    private sealed class NumberEntry(double value, DataPosition position, string? note)
    {
        public double Value { get; } = value;

        public DataPosition Position { get; } = position;

        public string? Note { get; } = note;
    }
}
