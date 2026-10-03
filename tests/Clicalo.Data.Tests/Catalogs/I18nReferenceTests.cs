using Clicalo.TestKit;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// Catalog names that are product text are referenced by message key, never written in the catalog
/// (IDI-002): every key must exist in Spanish and English.
/// </summary>
public sealed class I18nReferenceTests
{
    /// <summary>
    /// Keys the catalogs need that the handoff string files do not have yet; the i18n package adds them
    /// (Spanish / English): key groups «Modificadores / Modifiers» and «F1–F12», the ten colour categories
    /// (PQ-50), and the «Básicos / Basics» option of the starter kit with its description (user decision D2).
    /// </summary>
    private static readonly string[] NewKeys =
    [
        "kgMods",
        "kgFn",
        "catEdit",
        "catHist",
        "catFile",
        "catSel",
        "catWin",
        "catVoice",
        "catNav",
        "catFmt",
        "catWeb",
        "catText",
        "kitBasics",
        "kitBasicsD",
    ];

    [Fact]
    [Trait("Req", "IDI-002")]
    public void Label_keys_exist_in_the_handoff_strings_or_are_declared_as_new()
    {
        var handoff = Keys(Path.Combine(RepoPaths.Handoff, "data", "strings.es.json"));

        var unknown = LabelKeys()
            .Where(k => !handoff.Contains(k) && !NewKeys.Contains(k, StringComparer.Ordinal));

        unknown.ShouldBeEmpty();
        NewKeys
            .Where(handoff.Contains)
            .ShouldBeEmpty("a declared new key already exists; drop it from the list");
        NewKeys
            .Where(k => !LabelKeys().Contains(k, StringComparer.Ordinal))
            .ShouldBeEmpty("unused new key");
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "IDI-001")]
    [Trait("Req", "IDI-002")]
    public void Label_keys_exist_in_the_product_string_files(string language)
    {
        var path = Path.Combine(RepoPaths.Data, "i18n", $"strings.{language}.json");
        Assert.SkipUnless(
            File.Exists(path),
            "data/i18n is provided by the i18n work package; run after integration."
        );

        var strings = Keys(path);

        LabelKeys().Where(k => !strings.Contains(k)).ShouldBeEmpty();
    }

    private static HashSet<string> Keys(string path) =>
        CatalogFiles.LoadObject(path).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<string> LabelKeys()
    {
        IEnumerable<string> From(string file, string member, params string[] properties) =>
            CatalogFiles.LoadObject(file)[member]!
                .AsArray()
                .SelectMany(item => properties.Select(p => item![p]!.GetValue<string>()));

        return
        [
            .. From(CatalogFiles.Catalog("keys.json"), "groups", "labelKey"),
            .. From(CatalogFiles.Catalog("categories.json"), "categories", "labelKey"),
            .. From(CatalogFiles.Catalog("mouse.json"), "scrollSpeeds", "labelKey"),
            .. From(
                CatalogFiles.Catalog("touch-presets.json"),
                "presets",
                "labelKey",
                "descriptionKey"
            ),
            .. From(CatalogFiles.Catalog("sizes.json"), "sizes", "labelKey"),
            .. From(CatalogFiles.ContentFile("library.json"), "sections", "labelKey"),
            .. CatalogFiles.LoadObject(CatalogFiles.ContentFile("starter.json"))["options"]!
                .AsArray()
                .Where(option => option!["labelKey"] is not null)
                .SelectMany(option =>
                    new[] { "labelKey", "descriptionKey" }.Select(p =>
                        option![p]!.GetValue<string>()
                    )
                ),
        ];
    }
}
