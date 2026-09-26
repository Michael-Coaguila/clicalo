namespace Clicalo.Data.Tests.I18n;

public sealed class PlaceholderTests
{
    private static readonly HashSet<string> Declared = LoadDeclared();

    [Fact]
    [Trait("Req", "IDI-004")]
    public void Every_key_uses_the_same_placeholders_in_every_language()
    {
        var spanish = I18nData.PlaceholdersByKey("es");
        var english = I18nData.PlaceholdersByKey("en");

        foreach (var (key, names) in spanish)
        {
            english[key].ShouldBe(names, key);
        }
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "IDI-004")]
    public void Placeholders_have_semantic_names_declared_in_placeholders_json(string language)
    {
        foreach (var (key, text) in I18nData.Strings(language))
        {
            foreach (var name in I18nData.Placeholders(text))
            {
                Declared.Contains(name).ShouldBeTrue(key + " uses {" + name + "}");
                name.Length.ShouldBeGreaterThan(1, key + " still uses a one-letter marker");
            }
        }
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "IDI-004")]
    public void Plural_families_have_one_and_other_and_select_with_count(string language)
    {
        var families = I18nData
            .Strings(language)
            .Where(static e => I18nData.Category(e.Key) is not null)
            .GroupBy(static e => I18nData.BaseKey(e.Key), StringComparer.Ordinal);

        foreach (var family in families)
        {
            family
                .Select(static e => I18nData.Category(e.Key))
                .ShouldBe(["one", "other"], family.Key);
            family
                .SelectMany(static e => I18nData.Placeholders(e.Value))
                .Contains("count", StringComparer.Ordinal)
                .ShouldBeTrue(family.Key);
        }
    }

    [Fact]
    public void The_placeholder_vocabulary_has_closed_types()
    {
        using var document = I18nData.Json("placeholders.json");
        foreach (var placeholder in document.RootElement.EnumerateObject())
        {
            new[] { "text", "integer", "number" }
                .Contains(placeholder.Value.GetProperty("type").GetString(), StringComparer.Ordinal)
                .ShouldBeTrue(placeholder.Name);
            placeholder.Value.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
        }
    }

    private static HashSet<string> LoadDeclared()
    {
        using var document = I18nData.Json("placeholders.json");
        return document
            .RootElement.EnumerateObject()
            .Select(static p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
