using Clicalo.Generators.Localization;

namespace Clicalo.Generators.Tests.Localization;

/// <summary>The Roslyn-free building blocks shared by the generator and <c>cl i18n-check</c>.</summary>
public sealed class LocalizationCoreTests
{
    [Theory]
    [InlineData("Paso {index} de {total}", new[] { "index", "total" })]
    [InlineData("{count} teclas · se guarda solo", new[] { "count" })]
    [InlineData("Sin marcadores", new string[0])]
    [InlineData("Llaves {{literales}} y {app}", new[] { "app" })]
    [InlineData("{a1}{b2}", new[] { "a1", "b2" })]
    public void Template_syntax_extracts_named_placeholders(string text, string[] expected)
    {
        TemplateSyntax.TryParse(text, out var placeholders, out _).ShouldBeTrue();

        placeholders.Select(static p => p.Name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("abierto {", 8)]
    [InlineData("cerrado }", 8)]
    [InlineData("{Mayúscula}", 0)]
    [InlineData("{con espacio}", 0)]
    [InlineData("{}", 0)]
    [InlineData("{{ok}} }", 7)]
    public void Template_syntax_rejects_stray_braces_with_their_index(string text, int index)
    {
        TemplateSyntax.TryParse(text, out _, out var error).ShouldBeFalse();

        error.Index.ShouldBe(index);
    }

    [Theory]
    [InlineData("migT", "migT", null)]
    [InlineData("comboN_one", "comboN", "one")]
    [InlineData("comboN_other", "comboN", "other")]
    [InlineData("vh2_few", "vh2", "few")]
    public void Keys_split_into_base_and_plural_category(
        string key,
        string baseKey,
        string? category
    )
    {
        KeyNaming.TrySplit(key, out var actualBase, out var actualCategory).ShouldBeTrue();

        actualBase.ShouldBe(baseKey);
        actualCategory.ShouldBe(category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("_one")]
    [InlineData("a-b")]
    [InlineData("a_b_one")]
    [InlineData("a_One")]
    [InlineData("1a")]
    [InlineData("ñandú")]
    public void Invalid_keys_are_rejected(string key) =>
        KeyNaming.TrySplit(key, out _, out _).ShouldBeFalse();

    [Theory]
    [InlineData("migT", "MigT")]
    [InlineData("vh2", "Vh2")]
    [InlineData("rSingle", "RSingle")]
    [InlineData("Search", "Search")]
    public void Member_names_are_the_key_with_an_upper_case_first_letter(
        string key,
        string member
    ) => KeyNaming.ToMemberName(key).ShouldBe(member);

    [Fact]
    public void Json_positions_find_the_key_of_a_value_and_characters_inside_escaped_strings()
    {
        const string json = "{\n  \"k\\\"ey\" :  \"a\\u00e9\\\"{x}\"\n}";
        var positions = new JsonPositions(json);

        positions.KeyOf(2, 14).ShouldBe((2, 3));
        positions.CharInString(2, 14, 0).ShouldBe((2, 15));
        positions.CharInString(2, 14, 3).ShouldBe((2, 24));
    }

    [Fact]
    public void Json_positions_ignore_a_byte_order_mark_like_MiniJson()
    {
        const string json = "\uFEFF{\"k\": \"v\"}";
        var positions = new JsonPositions(json);

        positions.KeyOf(1, 7).ShouldBe((1, 2));
    }

    [Theory]
    [InlineData("es", true)]
    [InlineData("en", true)]
    [InlineData("pt-BR", true)]
    [InlineData("zh-Hant", true)]
    [InlineData("EN", false)]
    [InlineData("e", false)]
    [InlineData("es_ES", false)]
    public void Language_codes_follow_bcp47_shape(string code, bool valid) =>
        LocalizationFiles.IsValidLanguageCode(code).ShouldBe(valid);
}
