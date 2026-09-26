using static Clicalo.Generators.Tests.Localization.TestData;

namespace Clicalo.Generators.Tests.Localization;

/// <summary>Every i18n data error is a CLCI compiler error at its exact position in the JSON file.</summary>
public sealed class LocalizationDiagnosticsTests
{
    private const string EsFile = "strings.es.json";
    private const string EnFile = "strings.en.json";

    private static string Single(Dictionary<string, string> files) =>
        GeneratorHarness.Run(files).Single();

    private static List<string> All(Dictionary<string, string> files) =>
        [.. GeneratorHarness.Run(files).Diagnostics.Select(GeneratorOutput.Describe)];

    [Fact]
    public void CLCI001_invalid_json_is_reported_at_its_line_and_column() =>
        Single(With(EsFile, "{\n  \"search\": \"Buscar\",\n}"))
            .ShouldBe("CLCI001 strings.es.json(3,1)");

    [Fact]
    public void CLCI001_duplicate_keys_are_invalid_json() =>
        Single(Replace(EsFile, "\"zoom\": \"Zoom {ratio}\"", "\"search\": \"Otra\""))
            .ShouldBe("CLCI001 strings.es.json(5,3)");

    [Fact]
    [Trait("Req", "IDI-001")]
    public void CLCI002_a_key_missing_in_english_is_reported_at_the_spanish_key() =>
        Single(Replace(EnFile, "  \"createFor\": \"Create for {app}\",\n", string.Empty))
            .ShouldBe("CLCI002 strings.es.json(6,3)");

    [Fact]
    [Trait("Req", "IDI-001")]
    public void CLCI002_a_key_only_in_english_is_reported_at_the_english_key() =>
        Single(WithEntry(EnFile, "\"extra\": \"Extra\"")).ShouldBe("CLCI002 strings.en.json(8,3)");

    [Fact]
    [Trait("Req", "IDI-004")]
    public void CLCI003_different_placeholders_between_languages() =>
        Single(Replace(EnFile, "Create for {app}", "Create for {profiles}"))
            .ShouldBe("CLCI003 strings.en.json(6,3)");

    [Fact]
    [Trait("Req", "IDI-004")]
    public void CLCI004_an_unknown_placeholder_is_reported_at_its_brace() =>
        Single(Replace(EsFile, "Crear para {app}", "Crear para {a}"))
            .ShouldBe("CLCI004 strings.es.json(6,28)");

    [Fact]
    public void CLCI004_positions_account_for_json_escapes() =>
        Single(Replace(EsFile, "Crear para {app}", "Crear \\\"para\\\" {a}"))
            .ShouldBe("CLCI004 strings.es.json(6,32)");

    [Fact]
    public void CLCI005_an_empty_text_is_reported_at_the_value() =>
        Single(Replace(EsFile, "\"Buscar\"", "\"  \"")).ShouldBe("CLCI005 strings.es.json(2,13)");

    [Theory]
    [InlineData("\"bad-key\": \"Mal\"")]
    [InlineData("\"bad_plural\": \"Mal\"")]
    [InlineData("\"9lives\": \"Mal\"")]
    [InlineData("\"comboN_ONE\": \"Mal\"")]
    public void CLCI006_invalid_keys_are_reported_at_the_key(string entry) =>
        Single(WithEntry(EsFile, entry)).ShouldBe("CLCI006 strings.es.json(8,3)");

    [Fact]
    public void CLCI007_keys_that_differ_only_in_the_first_letter_collide()
    {
        var files = WithEntry(EsFile, "\"Search\": \"Buscar 2\"");
        files[EnFile] = WithEntry(EnFile, "\"Search\": \"Search 2\"")[EnFile];

        Single(files).ShouldBe("CLCI007 strings.es.json(8,3)");
    }

    [Fact]
    public void CLCI007_keys_must_not_produce_reserved_member_names()
    {
        var files = WithEntry(EsFile, "\"toString\": \"Texto\"");
        files[EnFile] = WithEntry(EnFile, "\"toString\": \"Text\"")[EnFile];

        Single(files).ShouldBe("CLCI007 strings.es.json(8,3)");
    }

    [Theory]
    [InlineData("Buscar {")]
    [InlineData("Buscar }")]
    [InlineData("Buscar {Count}")]
    [InlineData("Buscar {co unt}")]
    [InlineData("{{ok}} {")]
    public void CLCI008_malformed_braces_are_reported_at_the_brace(string text) =>
        Single(Replace(EsFile, "\"Buscar\"", "\"" + text + "\""))
            .ShouldBe("CLCI008 strings.es.json(2,21)");

    [Fact]
    public void Doubled_braces_are_literal_braces_not_placeholders() =>
        GeneratorHarness
            .Run(Replace(EsFile, "\"Buscar\"", "\"Buscar {{literal}}\""))
            .Diagnostics.ShouldBeEmpty();

    [Fact]
    public void CLCI009_a_plural_family_without_other_is_an_error() =>
        Single(Replace(EsFile, "  \"comboN_other\": \"{count} teclas\",\n", string.Empty))
            .ShouldBe("CLCI009 strings.es.json(3,3)");

    [Fact]
    public void CLCI009_a_plural_family_needs_every_category_of_the_language() =>
        Single(Replace(EnFile, "  \"comboN_one\": \"{count} key\",\n", string.Empty))
            .ShouldBe("CLCI009 strings.en.json(3,3)");

    [Fact]
    public void CLCI010_a_category_the_language_does_not_use_is_an_error() =>
        Single(WithEntry(EsFile, "\"comboN_few\": \"{count} teclas\""))
            .ShouldBe("CLCI010 strings.es.json(8,3)");

    [Fact]
    public void CLCI011_a_plural_family_must_select_with_count()
    {
        var files = WithEntry(EsFile, "\"fooBar_one\": \"Uno\", \"fooBar_other\": \"Varios\"");
        files[EnFile] = WithEntry(EnFile, "\"fooBar_one\": \"One\", \"fooBar_other\": \"Several\"")[
            EnFile
        ];

        All(files).ShouldBe(["CLCI011 strings.en.json(8,3)", "CLCI011 strings.es.json(8,3)"]);
    }

    [Fact]
    public void CLCI011_the_count_selector_must_be_numeric() =>
        All(
                With(
                    "placeholders.json",
                    Placeholders.Replace(
                        "\"count\": { \"type\": \"integer\"",
                        "\"count\": { \"type\": \"text\"",
                        StringComparison.Ordinal
                    )
                )
            )
            .ShouldBe(["CLCI011 strings.en.json(3,3)", "CLCI011 strings.es.json(3,3)"]);

    [Fact]
    public void CLCI012_a_key_plural_in_one_language_and_plain_in_the_other()
    {
        var files = Replace(
            EnFile,
            "\"search\": \"Search\",",
            "\"search_one\": \"{count} search\", \"search_other\": \"{count} searches\","
        );

        Single(files).ShouldBe("CLCI012 strings.en.json(2,3)");
    }

    [Fact]
    public void CLCI012_a_key_both_plain_and_plural_in_the_same_language() =>
        Single(WithEntry(EsFile, "\"search_other\": \"{count} búsquedas\""))
            .ShouldBe("CLCI012 strings.es.json(2,3)");

    [Fact]
    public void CLCI013_locales_json_is_required() =>
        Single(With("locales.json", null)).ShouldBe("CLCI013");

    [Fact]
    public void CLCI013_the_default_language_must_be_declared() =>
        Single(
                With(
                    "locales.json",
                    Locales.Replace(
                        "\"default\": \"es\"",
                        "\"default\": \"fr\"",
                        StringComparison.Ordinal
                    )
                )
            )
            .ShouldBe("CLCI013 locales.json(2,14)");

    [Fact]
    [Trait("Req", "IDI-006")]
    public void CLCI013_a_declared_language_needs_its_strings_file() =>
        Single(With(EnFile, null)).ShouldBe("CLCI013 locales.json(5,5)");

    [Fact]
    [Trait("Req", "IDI-006")]
    public void CLCI013_a_strings_file_needs_its_language_in_locales_json()
    {
        var files = TestData.Valid();
        files["strings.fr.json"] = En;

        Single(files).ShouldBe("CLCI013 strings.fr.json(1,1)");
    }

    [Fact]
    public void CLCI013_strings_files_are_named_with_a_language_code()
    {
        var files = TestData.Valid();
        files["strings.EN.json"] = En;

        Single(files).ShouldBe("CLCI013 strings.EN.json(1,1)");
    }

    [Theory]
    [InlineData(
        "\"shortName\": \"ES\"",
        "\"shortName\": \"ES\", \"extra\": 1",
        "CLCI013 locales.json(4,82)"
    )]
    [InlineData(
        "\"plural\": { \"one\": \"n = 1\" }",
        "\"plural\": { \"other\": \"n = 1\" }",
        "CLCI013 locales.json(4,119)"
    )]
    [InlineData("\"decimalSeparator\": \",\", ", "", "CLCI013 locales.json(4,5)")]
    public void CLCI013_locale_entries_are_validated(
        string oldValue,
        string newValue,
        string expected
    ) =>
        Single(With("locales.json", Locales.Replace(oldValue, newValue, StringComparison.Ordinal)))
            .ShouldBe(expected);

    [Fact]
    public void CLCI014_placeholders_json_is_required() =>
        Single(With("placeholders.json", null)).ShouldBe("CLCI014");

    [Fact]
    public void CLCI014_placeholder_types_are_closed() =>
        Single(
                With(
                    "placeholders.json",
                    Placeholders.Replace(
                        "\"type\": \"number\"",
                        "\"type\": \"float\"",
                        StringComparison.Ordinal
                    )
                )
            )
            .ShouldBe("CLCI014 placeholders.json(5,22)");

    [Fact]
    public void CLCI015_values_must_be_strings() =>
        Single(Replace(EsFile, "\"Buscar\"", "42")).ShouldBe("CLCI015 strings.es.json(2,13)");

    [Fact]
    public void CLCI015_a_strings_file_is_an_object() =>
        Single(With(EsFile, "[\"Buscar\"]")).ShouldBe("CLCI015 strings.es.json(1,1)");
}
