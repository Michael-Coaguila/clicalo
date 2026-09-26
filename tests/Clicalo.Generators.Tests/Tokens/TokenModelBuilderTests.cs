using System.Globalization;
using Clicalo.Generators.Tokens;

namespace Clicalo.Generators.Tests.Tokens;

/// <summary>Every CLCT data error is reported with its id and its exact position in the JSON file.</summary>
public sealed class TokenModelBuilderTests
{
    private const string Palettes = TokenFiles.ThemePalettes;
    private const string Extra = TokenFiles.ExtraTokens;
    private const string Pairs = TokenFiles.ContrastPairs;
    private const string SystemMap = TokenFiles.HighContrastSystemMap;

    [Fact]
    [Trait("Req", "TEM-004")]
    public void The_real_data_has_no_errors_and_can_be_emitted()
    {
        var model = TokenTestData.Build(TokenTestData.Files);

        model.Issues.Select(Describe).ShouldBeEmpty();
        model.CanEmit.ShouldBeTrue();
        model.Themes.Select(theme => theme.Name).ShouldBe(["Dark", "Light", "HighContrast"]);
        model.ColorTokens.Count.ShouldBe(27);
        model.Categories.Count.ShouldBe(10);
        model.Motion.Count.ShouldBe(8);
        model.SystemColorByToken.Count.ShouldBe(27);
    }

    [Fact]
    public void Resolved_colors_carry_corrections_and_aliases()
    {
        var model = TokenTestData.Build(TokenTestData.Files);
        var light = model.Themes.Single(theme =>
            string.Equals(theme.Key, "light", StringComparison.Ordinal)
        );
        var dark = model.Themes.Single(theme =>
            string.Equals(theme.Key, "dark", StringComparison.Ordinal)
        );

        light.Colors["accent"].Value.ToArgbHex().ShouldBe("#FF006885");
        light.Colors["accent"].Note.ShouldBe("TEM-004: corrected from oklch(0.50 0.11 220)");
        light.Colors["focusRing"].Value.ShouldBe(light.Colors["accent"].Value);
        light.Colors["focusRing"].Note.ShouldBe("Same as accent.");
        dark.BorderThickness.ShouldBe(1d);
        model.Themes.Single(theme => theme.IsHighContrast).BorderThickness.ShouldBe(2d);
        dark.CategoryTints["nav"].Value.ToArgbHex().ShouldBe("#FFB1C8FF");
        dark.CategoryTints["nav"].Note.ShouldBe("TEM-004: corrected from 0.82");
    }

    [Fact]
    public void CLCT001_points_at_the_offending_character_of_an_invalid_color()
    {
        var files = TokenTestData.With(
            Palettes,
            "\"card\": \"oklch(0.27",
            "\"card\": \"oklch(1.27"
        );

        var issue = TokenTestData.Build(files).Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.InvalidColor);
        issue.Path.ShouldBe(TokenTestData.Directory + Palettes);
        (issue.Line, issue.Column).ShouldBe(
            TokenTestData.PositionOf(files[Palettes], "1.27 0.014 260")
        );
        issue.Message.ShouldContain("lightness must be between 0 and 1");
    }

    [Fact]
    public void CLCT001_reports_an_invalid_border_inside_the_shorthand()
    {
        var files = TokenTestData.With(
            Palettes,
            "1px solid oklch(1 0 0 / 0.09)",
            "1px dotted oklch(1 0 0 / 0.09)"
        );

        var issue = TokenTestData.Build(files).Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.InvalidColor);
        (issue.Line, issue.Column).ShouldBe(TokenTestData.PositionOf(files[Palettes], "dotted"));
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void CLCT002_reports_a_pair_below_its_minimum_at_the_background_entry()
    {
        var files = TokenTestData.With(
            Palettes,
            "\"muted\": \"oklch(0.45 0.015 260)\"",
            "\"muted\": \"oklch(0.62 0.015 260)\""
        );

        var issues = TokenTestData.Build(files).Issues;

        issues.ShouldAllBe(issue =>
            string.Equals(issue.Id, TokenIds.ContrastTooLow, StringComparison.Ordinal)
        );
        var issue = issues.First(i =>
            i.Message.Contains("'muted' on 'panel' in theme 'light'", StringComparison.Ordinal)
        );
        issue.Path.ShouldBe(TokenTestData.Directory + Pairs);
        (issue.Line, issue.Column).ShouldBe(
            TokenTestData.PositionOf(files[Pairs], "\"panel\"", after: "\"foreground\": \"muted\"")
        );
        issue.Message.ShouldContain("below the required 4.5:1");
        issue.Message.ShouldContain("worst case over the backdrop #000000");
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void CLCT002_lists_the_failing_categories_of_a_category_pair()
    {
        var files = TokenTestData.With(Extra, "\"to\": 0.468", "\"to\": 0.5");

        var issue = TokenTestData
            .Build(files)
            .Issues.Single(i =>
                i.Message.Contains(
                    "'categoryTint' on 'categoryWash over categoryWash over panel' in theme 'light'",
                    StringComparison.Ordinal
                )
            );

        issue.Id.ShouldBe(TokenIds.ContrastTooLow);
        issue.Message.ShouldContain("is below 4.5:1 for the categories ");
        issue.Message.ShouldContain("voice (3.88:1)");
    }

    [Fact]
    [Trait("Req", "TEM-003")]
    public void CLCT003_reports_a_color_that_gamut_mapping_moves_too_far()
    {
        var files = TokenTestData.With(
            Palettes,
            "\"accent\": \"oklch(0.80 0.11 200)\"",
            "\"accent\": \"oklch(0.80 0.3 200)\""
        );

        var issue = TokenTestData
            .Build(files)
            .Issues.Single(i => string.Equals(i.Id, TokenIds.OutOfGamut, StringComparison.Ordinal));

        issue.Path.ShouldBe(TokenTestData.Directory + Palettes);
        (issue.Line, issue.Column).ShouldBe(
            TokenTestData.PositionOf(files[Palettes], "\"oklch(0.80 0.3 200)\"")
        );
        issue.Message.ShouldContain("'accent' in theme 'dark'");
    }

    [Fact]
    [Trait("Req", "TEM-003")]
    public void CLCT003_also_covers_the_derived_category_colors()
    {
        var files = TokenTestData.With(Extra, "\"tintChroma\": 0.12", "\"tintChroma\": 0.3");

        var issues = TokenTestData
            .Build(files)
            .Issues.Where(i => string.Equals(i.Id, TokenIds.OutOfGamut, StringComparison.Ordinal))
            .ToList();

        issues.ShouldNotBeEmpty();
        issues.ShouldAllBe(issue => issue.Message.Contains("category", StringComparison.Ordinal));
    }

    [Fact]
    public void CLCT004_reports_malformed_json_at_its_position()
    {
        var files = TokenTestData.With(Extra, "\"compact\": 6,", "\"compact\": 6,,");

        var issue = TokenTestData.Build(files).Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.MalformedFile);
        issue.Path.ShouldBe(TokenTestData.Directory + Extra);
        issue.Line.ShouldBe(TokenTestData.PositionOf(files[Extra], "6,,").Line);
    }

    [Fact]
    public void CLCT004_reports_a_member_of_the_wrong_kind()
    {
        var files = TokenTestData.With(Extra, "\"tintChroma\": 0.12", "\"tintChroma\": \"0.12\"");

        var issue = TokenTestData.Build(files).Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.MalformedFile);
        (issue.Line, issue.Column).ShouldBe(TokenTestData.PositionOf(files[Extra], "\"0.12\""));
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void CLCT004_rejects_a_minimum_below_wcag_aa()
    {
        var files = TokenTestData.With(Pairs, "\"text\": 4.5", "\"text\": 4");

        var issue = TokenTestData.Build(files).Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.MalformedFile);
        issue.Message.ShouldContain("WCAG 2.x AA");
    }

    [Fact]
    [Trait("Req", "TEM-002")]
    public void CLCT005_reports_a_token_missing_in_one_theme()
    {
        var files = TokenTestData.With(Palettes, "\"field\": \"#000000\",\n", string.Empty);

        var issues = TokenTestData.Build(files).Issues;

        issues.ShouldContain(i =>
            string.Equals(i.Id, TokenIds.UnknownToken, StringComparison.Ordinal)
            && i.Message.Contains(
                "'hc' does not define the token 'field'",
                StringComparison.Ordinal
            )
        );
    }

    [Theory]
    [InlineData(
        Pairs,
        "\"foreground\": \"text\"",
        "\"foreground\": \"txt\"",
        "Unknown color token 'txt'"
    )]
    [InlineData(
        Pairs,
        "\"desk\": \"Escritorio",
        "\"deskX\": \"Escritorio",
        "'desk' is neither measured"
    )]
    [InlineData(
        Extra,
        "\"focusRing\": \"accent\"",
        "\"focusRing\": \"accnt\"",
        "refers to 'accnt'"
    )]
    [InlineData(Extra, "\"warnText\": \"warn\"", "\"warnText\": \"warnText\"", "cycle")]
    [InlineData(Extra, "\"light\": \"Light\"", "\"lite\": \"Light\"", "'light' has no C# name")]
    [InlineData(SystemMap, "\"desk\": \"window\",", "", "does not map the token 'desk'")]
    [InlineData(
        SystemMap,
        "\"wpf\": \"WindowColor\"",
        "\"wpf\": \"WindowColour\"",
        "not a System.Windows.SystemColors"
    )]
    [InlineData(
        SystemMap,
        "\"tint\": \"highlight\"",
        "\"tint\": \"hilite\"",
        "'hilite' is not declared"
    )]
    public void CLCT005_reports_unknown_or_unmapped_names(
        string file,
        string find,
        string replace,
        string message
    )
    {
        var issues = TokenTestData.Build(TokenTestData.With(file, find, replace)).Issues;

        issues.ShouldContain(i =>
            string.Equals(i.Id, TokenIds.UnknownToken, StringComparison.Ordinal)
            && i.Message.Contains(message, StringComparison.Ordinal)
        );
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void CLCT006_reports_a_correction_whose_original_value_changed()
    {
        var files = TokenTestData.With(
            Palettes,
            "\"warn\": \"oklch(0.58 0.13 70)\"",
            "\"warn\": \"oklch(0.57 0.13 70)\""
        );

        var issue = TokenTestData.Build(files).Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.StaleCorrection);
        issue.Path.ShouldBe(TokenTestData.Directory + Extra);
        (issue.Line, issue.Column).ShouldBe(
            TokenTestData.PositionOf(files[Extra], "\"oklch(0.58 0.13 70)\"", after: "\"warn\": {")
        );
        issue.Message.ShouldContain("'warn' in theme 'light'");
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void CLCT006_also_guards_numeric_corrections()
    {
        var issue = TokenTestData
            .Build(TokenTestData.With(Palettes, "\"tintL\": 0.82", "\"tintL\": 0.83"))
            .Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.StaleCorrection);
        issue.Message.ShouldContain("'tintL' in theme 'dark'");
    }

    [Fact]
    public void CLCT007_reports_a_missing_token_file()
    {
        var issue = TokenTestData
            .Build(TokenTestData.Without(TokenFiles.Motion))
            .Issues.ShouldHaveSingleItem();

        issue.Id.ShouldBe(TokenIds.MissingFile);
        issue.Path.ShouldBeNull();
        issue.Message.ShouldContain("data/tokens/motion.json");
    }

    [Fact]
    public void Structural_errors_prevent_emission_but_contrast_errors_do_not()
    {
        TokenTestData.Build(TokenTestData.Without(TokenFiles.Motion)).CanEmit.ShouldBeFalse();
        TokenTestData
            .Build(
                TokenTestData.With(
                    Palettes,
                    "\"muted\": \"oklch(0.45 0.015 260)\"",
                    "\"muted\": \"oklch(0.62 0.015 260)\""
                )
            )
            .CanEmit.ShouldBeTrue();
    }

    [Fact]
    public void Every_descriptor_is_an_error_with_a_help_link()
    {
        var descriptors = TokenDiagnostics.All.ToList();

        descriptors
            .Select(d => d.Id)
            .ShouldBe([
                "CLCT001",
                "CLCT002",
                "CLCT003",
                "CLCT004",
                "CLCT005",
                "CLCT006",
                "CLCT007",
            ]);
        descriptors.ShouldAllBe(d =>
            d.DefaultSeverity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        );
        descriptors.ShouldAllBe(d =>
            d.HelpLinkUri.EndsWith(
                "design-tokens.md#" + d.Id.ToLowerInvariant(),
                StringComparison.Ordinal
            )
        );
    }

    private static string Describe(TokenIssue issue) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{issue.Id} {issue.Path}({issue.Line},{issue.Column}): {issue.Message}"
        );
}
