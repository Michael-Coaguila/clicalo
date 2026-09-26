using System.Text.Json;

namespace Clicalo.Data.Tests.I18n;

public sealed class LocalesTests
{
    [Fact]
    public void Spanish_is_the_default_language()
    {
        using var document = I18nData.Json("locales.json");

        document.RootElement.GetProperty("default").GetString().ShouldBe("es");
    }

    [Theory]
    [InlineData("es", ",")]
    [InlineData("en", ".")]
    [Trait("Req", "IDI-004")]
    public void Decimals_use_a_comma_in_Spanish_and_a_point_in_English(
        string code,
        string separator
    ) => Locale(code).GetProperty("decimalSeparator").GetString().ShouldBe(separator);

    [Fact]
    [Trait("Req", "IDI-006")]
    public void Every_declared_language_has_a_strings_file_and_every_strings_file_a_language()
    {
        using var document = I18nData.Json("locales.json");
        var declared = document
            .RootElement.GetProperty("locales")
            .EnumerateArray()
            .Select(static l => l.GetProperty("code").GetString()!)
            .Order(StringComparer.Ordinal);
        var files = Directory
            .EnumerateFiles(I18nData.Directory, "strings.*.json")
            .Select(static f => Path.GetFileName(f)["strings.".Length..^".json".Length])
            .Order(StringComparer.Ordinal);

        files.ShouldBe(declared);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "IDI-006")]
    public void Every_language_names_itself_for_the_language_picker(string code)
    {
        var locale = Locale(code);

        locale.GetProperty("nativeName").GetString().ShouldNotBeNullOrWhiteSpace();
        locale.GetProperty("shortName").GetString().ShouldBe(code.ToUpperInvariant());
    }

    private static JsonElement Locale(string code)
    {
        using var document = I18nData.Json("locales.json");
        return document
            .RootElement.GetProperty("locales")
            .EnumerateArray()
            .Single(l =>
                string.Equals(l.GetProperty("code").GetString(), code, StringComparison.Ordinal)
            )
            .Clone();
    }
}
