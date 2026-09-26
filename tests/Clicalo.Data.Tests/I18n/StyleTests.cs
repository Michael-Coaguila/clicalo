namespace Clicalo.Data.Tests.I18n;

/// <summary>Typography by language (IDI-004): «» in Spanish, “” in English, never straight double quotes.</summary>
public sealed class StyleTests
{
    [Fact]
    [Trait("Req", "IDI-004")]
    public void Spanish_quotes_with_angle_quotes() =>
        I18nData
            .Strings("es")
            .Where(static e => e.Value.IndexOfAny(['“', '”', '"']) >= 0)
            .Select(static e => e.Key)
            .ShouldBeEmpty();

    [Fact]
    [Trait("Req", "IDI-004")]
    public void English_quotes_with_curly_quotes() =>
        I18nData
            .Strings("en")
            .Where(static e => e.Value.IndexOfAny(['«', '»', '"']) >= 0)
            .Select(static e => e.Key)
            .ShouldBeEmpty();

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "TEM-008")]
    public void The_product_name_keeps_its_accent(string language) =>
        I18nData
            .Strings(language)
            .Where(static e => e.Value.Contains("Clicalo", StringComparison.Ordinal))
            .Select(static e => e.Key)
            .ShouldBeEmpty();
}
