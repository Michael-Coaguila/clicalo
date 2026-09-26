using System.Globalization;
using System.Text.Json.Nodes;
using Clicalo.Design.Math;
using Clicalo.TestKit;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>
/// The design tokens of <c>data/tokens</c>, checked independently of the generator: completeness, categories,
/// contrast on the real composite, documented minimal corrections, motion, focus ring and the Windows
/// high-contrast map.
/// </summary>
public sealed class TokenDataTests
{
    private static readonly Lazy<TokenDataSet> Data = new(TokenDataSet.Load);
    private static readonly Lazy<ContrastPairSet> Pairs = new(ContrastPairSet.Load);

    [Fact]
    [Trait("Req", "TEM-002")]
    public void Theme_palettes_are_a_faithful_copy_of_the_design_package()
    {
        var copy = JsonNode.Parse(
            File.ReadAllText(Path.Combine(TokenDataSet.Directory, "theme-palettes.json"))
        );
        var original = JsonNode.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Handoff, "data", "theme-palettes.json"))
        );

        copy!.ToJsonString().ShouldBe(original!.ToJsonString());
    }

    [Fact]
    [Trait("Req", "TEM-002")]
    public void Every_theme_defines_every_token_including_the_ones_TEM_002_adds()
    {
        string[] required =
        [
            "desk",
            "panel",
            "win",
            "side",
            "card",
            "cardHi",
            "text",
            "muted",
            "line",
            "accent",
            "accentWash",
            "onAccent",
            "warn",
            "warnWash",
            "border",
            "field",
            "danger",
            "onDanger",
            "success",
            "onWarn",
            "scrim",
            "shadow",
            "focusRing",
        ];
        var data = Data.Value;

        data.Themes.ShouldBe(["dark", "light", "hc"]);
        foreach (var theme in data.Themes)
        {
            foreach (var token in required.Concat(data.Tokens))
            {
                data.Defines(theme, token).ShouldBeTrue($"theme '{theme}' lacks '{token}'");
            }
        }
    }

    [Fact]
    [Trait("Req", "TEM-003")]
    public void Categories_use_the_hues_and_formula_of_TEM_003()
    {
        var categories = Data.Value.Categories;

        categories.Hues.ShouldBe(
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["edit"] = 230,
                ["hist"] = 60,
                ["file"] = 150,
                ["sel"] = 300,
                ["win"] = 25,
                ["voice"] = 190,
                ["nav"] = 270,
                ["fmt"] = 330,
                ["web"] = 200,
                ["text"] = 120,
            }
        );
        categories.TintChroma.ShouldBe(0.12);
        categories.WashLightness.ShouldBe(0.72);
        categories.WashChroma.ShouldBe(0.12);
        Data.Value.Number("dark", TokenDataSet.WashAlpha).ShouldBe(0.22);
        Data.Value.Number("light", TokenDataSet.WashAlpha).ShouldBe(0.16);
        Data.Value.Tint("hc", "edit").ShouldBe(new Rgba8(0xFF, 0xE6, 0x00));
        Data.Value.Wash("hc", "edit").ShouldBe(new Rgba8(0x33, 0x30, 0x00));
    }

    [Fact]
    [Trait("Req", "TEM-003")]
    public void Every_out_of_gamut_color_is_mapped_within_the_declared_delta()
    {
        using var extra = TokenDataSet.Read("extra-tokens.json");
        var limit = extra
            .RootElement.GetProperty("gamutMapping")
            .GetProperty("maxDeltaEOK")
            .GetDouble();
        var data = Data.Value;
        var outOfGamut = 0;
        foreach (var theme in data.Themes)
        {
            var colors = data.Tokens.Select(token => data.Source(theme, token)).ToList();
            if (!data.IsHighContrast(theme))
            {
                var categories = data.Categories;
                foreach (var hue in categories.Hues.Values)
                {
                    colors.Add(
                        FormattableString.Invariant(
                            $"oklch({data.Number(theme, TokenDataSet.TintLightness)} {categories.TintChroma} {hue})"
                        )
                    );
                    colors.Add(
                        FormattableString.Invariant(
                            $"oklch({categories.WashLightness} {categories.WashChroma} {hue} / {data.Number(theme, TokenDataSet.WashAlpha)})"
                        )
                    );
                }
            }

            foreach (
                var color in colors
                    .Select(TokenDataSet.Parse)
                    .Where(c => c.Notation == CssColorNotation.Oklch)
            )
            {
                var mapped = color.MapToSrgb();
                mapped.DeltaEok.ShouldBeLessThanOrEqualTo(limit, $"{theme}: {color}");
                outOfGamut += mapped.WasInGamut ? 0 : 1;
            }
        }

        outOfGamut.ShouldBeGreaterThan(0, "the mapping must be exercised by real data");
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    [Trait("Req", "NFR-007")]
    public void Every_contrast_pair_meets_its_minimum_in_every_theme()
    {
        var results = Pairs.Value.EvaluateAll(Data.Value).ToList();

        results.Count.ShouldBeGreaterThan(300);
        results.Where(result => !result.Passes).Select(result => result.ToString()).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void Minimums_are_never_below_wcag_aa()
    {
        Pairs.Value.TextMinimum.ShouldBeGreaterThanOrEqualTo(Wcag.MinimumTextContrast);
        Pairs.Value.GraphicMinimum.ShouldBeGreaterThanOrEqualTo(Wcag.MinimumGraphicContrast);
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void Translucent_surfaces_are_measured_over_the_darkest_and_lightest_desktops() =>
        Pairs.Value.Backdrops.ShouldBe([new Rgba8(0, 0, 0), new Rgba8(255, 255, 255)]);

    [Fact]
    [Trait("Req", "TEM-004")]
    public void The_light_theme_failures_cited_by_TEM_004_fail_without_their_corrections()
    {
        var original = Data.Value;
        foreach (
            var correction in TokenDataSet
                .ReadCorrections()
                .Where(c => string.Equals(c.Theme, "light", StringComparison.Ordinal))
        )
        {
            original = original.With(correction.Theme, correction.Token, correction.From);
        }

        var failing = Pairs
            .Value.Evaluate(original, "light")
            .Where(result => !result.Passes)
            .Select(result => result.Pair.Foreground + " on " + result.Background)
            .ToHashSet(StringComparer.Ordinal);

        // warn used as text (warnText), accent on cardHi, danger and the line borders.
        string[] cited =
        [
            "warnText on card",
            "accent on cardHi",
            "onDanger on danger",
            "dangerText on card",
            "line on panel",
        ];
        cited.Where(pair => !failing.Contains(pair)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void Every_correction_is_documented_keeps_the_hue_and_is_the_smallest_that_passes()
    {
        var data = Data.Value;
        var corrections = TokenDataSet.ReadCorrections();

        corrections.ShouldNotBeEmpty();
        foreach (var correction in corrections)
        {
            correction.Requirement.ShouldBe("TEM-004", correction.ToString());
            correction.Reason.ShouldNotBeNullOrWhiteSpace(correction.ToString());

            var stepBack = StepBack(correction);
            var failures = Pairs.Value.Evaluate(
                data.With(correction.Theme, correction.Token, stepBack),
                correction.Theme
            );

            failures
                .Any(result => !result.Passes)
                .ShouldBeTrue($"{correction} is not minimal: {stepBack} also passes");
        }
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void High_contrast_is_opaque_except_for_the_absent_shadow()
    {
        var data = Data.Value;
        foreach (var token in data.Tokens)
        {
            var alpha = data.Color("hc", token).A;

            alpha.ShouldBe(
                string.Equals(token, "shadow", StringComparison.Ordinal) ? (byte)0 : byte.MaxValue,
                token
            );
        }
    }

    [Fact]
    [Trait("Req", "NFR-007")]
    public void Every_token_has_a_contrast_decision()
    {
        var pairs = Pairs.Value;
        var covered = pairs
            .Pairs.SelectMany(pair =>
                pair.Backgrounds.SelectMany(ContrastPairSet.Layers).Append(pair.Foreground)
            )
            .Concat(pairs.Decorative.Keys)
            .ToHashSet(StringComparer.Ordinal);

        Data.Value.Tokens.Where(token => !covered.Contains(token)).ShouldBeEmpty();
        pairs.Decorative.Values.ShouldAllBe(reason => !string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    [Trait("Req", "TEM-006")]
    public void Reduced_motion_removes_every_animation_but_keeps_the_flash_as_a_color_change()
    {
        using var motion = TokenDataSet.Read("motion.json");
        var durations = TokenDataSet
            .DataMembers(motion.RootElement.GetProperty("durations"))
            .ToDictionary(
                member => member.Name,
                member =>
                    (
                        Ms: member.Value.GetProperty("ms").GetInt32(),
                        Reduced: member.Value.GetProperty("reducedMs").GetInt32()
                    ),
                StringComparer.Ordinal
            );

        durations["panelOpacity"].ShouldBe((350, 0));
        durations["flash"].ShouldBe((240, 240));
        durations
            .Where(pair => !string.Equals(pair.Key, "flash", StringComparison.Ordinal))
            .ShouldAllBe(pair => pair.Value.Reduced == 0);
    }

    [Fact]
    [Trait("Req", "TEM-009")]
    public void The_focus_ring_is_3_px_at_2_px_in_accent_and_yellow_in_high_contrast()
    {
        using var extra = TokenDataSet.Read("extra-tokens.json");
        var ring = extra.RootElement.GetProperty("focusRing");
        var data = Data.Value;

        ring.GetProperty("thickness").GetDouble().ShouldBe(3d);
        ring.GetProperty("offset").GetDouble().ShouldBe(2d);
        foreach (var theme in data.Themes)
        {
            data.Color(theme, "focusRing").ShouldBe(data.Color(theme, "accent"));
        }

        data.Color("hc", "focusRing").ShouldBe(new Rgba8(0xFF, 0xE6, 0x00));
    }

    [Fact]
    [Trait("Req", "TEM-001")]
    public void The_windows_contrast_map_covers_every_token_and_keeps_every_pair_legible()
    {
        using var map = TokenDataSet.Read("hc-system-map.json");
        var root = map.RootElement;
        var system = TokenDataSet
            .DataMembers(root.GetProperty("tokens"))
            .ToDictionary(
                member => member.Name,
                member => member.Value.GetString()!,
                StringComparer.Ordinal
            );
        var categories = root.GetProperty("categories");
        system[ContrastPairSet.CategoryTint] = categories.GetProperty("tint").GetString()!;
        system[ContrastPairSet.CategoryWash] = categories.GetProperty("wash").GetString()!;
        var guaranteed = root.GetProperty("guaranteedPairs")
            .EnumerateArray()
            .Select(pair =>
                pair.GetProperty("foreground").GetString()
                + " on "
                + pair.GetProperty("background").GetString()
            )
            .ToHashSet(StringComparer.Ordinal);

        Data.Value.Tokens.Where(token => !system.ContainsKey(token)).ShouldBeEmpty();
        foreach (var pair in Pairs.Value.Pairs)
        {
            foreach (var background in pair.Backgrounds)
            {
                // The layer directly under the foreground is what it is read against.
                var mapped =
                    system[pair.Foreground]
                    + " on "
                    + system[ContrastPairSet.Layers(background)[0]];

                guaranteed
                    .Contains(mapped)
                    .ShouldBeTrue($"{pair.Foreground} on {background} maps to {mapped}");
            }
        }
    }

    /// <summary>The corrected value moved one resolution step back toward the original.</summary>
    private static string StepBack(Correction correction)
    {
        if (correction.Token is TokenDataSet.TintLightness or TokenDataSet.WashAlpha)
        {
            var to = double.Parse(correction.To, CultureInfo.InvariantCulture);
            var from = double.Parse(correction.From, CultureInfo.InvariantCulture);
            return (to + (Math.Sign(from - to) * 0.001)).ToString(
                "0.###",
                CultureInfo.InvariantCulture
            );
        }

        var original = TokenDataSet.Parse(correction.From).Oklch;
        var corrected = TokenDataSet.Parse(correction.To).Oklch;
        corrected.C.ShouldBe(original.C, correction.ToString());
        corrected.H.ShouldBe(original.H, correction.ToString());
        if (corrected.L.Equals(original.L))
        {
            // Neutral translucent lines cannot move in lightness: the alpha is corrected instead.
            var alpha = corrected.Alpha + (Math.Sign(original.Alpha - corrected.Alpha) * 0.01);
            return new Oklch(
                corrected.L,
                corrected.C,
                corrected.H,
                Math.Round(alpha, 2)
            ).ToString();
        }

        corrected.Alpha.ShouldBe(original.Alpha, correction.ToString());
        var lightness = corrected.L + (Math.Sign(original.L - corrected.L) * 0.001);
        return corrected.WithLightness(Math.Round(lightness, 3)).ToString();
    }
}
