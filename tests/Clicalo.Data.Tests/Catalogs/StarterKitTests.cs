using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// The starter kit of <c>data/content/starter.json</c> (user decision D2 of 2026-10-03): «Basics» marked by default
/// and every template offered once, unmarked, so a first start and «Skip» install only the universal shortcuts.
/// </summary>
[Trait("Req", "BIE-006")]
[Trait("Req", "CAT-003")]
public sealed class StarterKitTests
{
    /// <summary>The action kinds the starter reader installs (Infrastructure.Catalogs.ContentShortcutReader).</summary>
    private static readonly string[] InstallableKinds =
    [
        "tap",
        "hold",
        "toggle",
        "mouse",
        "macro",
        "web",
    ];

    private static IReadOnlyList<JsonObject> Options =>
        [
            .. CatalogFiles.LoadObject(CatalogFiles.ContentFile("starter.json"))["options"]!
                .AsArray()
                .Select(o => o!.AsObject()),
        ];

    [Fact]
    [Trait("Req", "BIE-003")]
    public void Basics_is_the_first_option_and_the_only_one_marked()
    {
        var options = Options;
        var basics = options.Where(o => Ordinal.Is(Kind(o), "basics")).ToList();

        basics.ShouldHaveSingleItem();
        options[0].ShouldBeSameAs(basics[0], "«Basics» opens the list");
        basics[0]["selected"]!.GetValue<bool>().ShouldBeTrue();
        options
            .Where(o => Ordinal.Is(Kind(o), "template"))
            .ShouldAllBe(o => !o["selected"]!.GetValue<bool>(), "templates are offered unmarked");
    }

    [Fact]
    [Trait("Req", "CAT-006")]
    public void Every_template_is_offered_exactly_once()
    {
        var offered = Options
            .Where(o => Ordinal.Is(Kind(o), "template"))
            .Select(o => o["id"]!.GetValue<string>())
            .ToList();

        offered.ShouldBeUnique(StringComparer.Ordinal);
        offered
            .Order(StringComparer.Ordinal)
            .ShouldBe(
                CatalogFiles
                    .TemplateFiles()
                    .Select(Path.GetFileNameWithoutExtension)
                    .Order(StringComparer.Ordinal)!
            );
        Options.Select(o => o["id"]!.GetValue<string>()).ShouldBeUnique(StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Req", "LOG-006")]
    public void The_content_of_the_kit_only_uses_kinds_the_starter_installs()
    {
        ContentCatalog
            .Lists.Where(l =>
                l.Name.StartsWith("seed:", StringComparison.Ordinal)
                || l.Name.StartsWith("template:", StringComparison.Ordinal)
            )
            .SelectMany(l => l.Shortcuts)
            .Where(s => !InstallableKinds.Contains(s.Type, StringComparer.Ordinal))
            .Select(s => s.Where)
            .ShouldBeEmpty("a kind the starter reader does not install would be left out silently");
    }

    private static string Kind(JsonObject option) => option["kind"]!.GetValue<string>();
}
