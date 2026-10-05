using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// Integrity of <c>common-actions.json</c> (EJE-018, decision D4 of the user): every exception names known families and
/// cites official sources, every source is cited, a process belongs to one family, and the seed and the library install
/// each common action with its standard combination, so the engine recognizes them.
/// </summary>
[Trait("Req", "EJE-018")]
public sealed class CommonActionsCatalogTests
{
    private static readonly JsonObject File = CatalogFiles.LoadObject(
        CatalogFiles.Catalog("common-actions.json")
    );

    [Fact]
    public void Every_exception_names_known_families_and_cites_known_sources()
    {
        var families = Ids("families");
        var sources = Ids("sources");
        var cited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var exception in Exceptions())
        {
            foreach (var family in Strings(exception["families"]))
            {
                families.Contains(family).ShouldBeTrue(family);
            }

            foreach (var source in Strings(exception["sources"]))
            {
                sources.Contains(source).ShouldBeTrue(source);
                cited.Add(source);
            }
        }

        cited.ShouldBe(sources, ignoreOrder: true);
    }

    [Fact]
    public void A_process_belongs_to_one_family_and_an_action_id_appears_once()
    {
        var processes = File["families"]!
            .AsArray()
            .SelectMany(static f => Strings(f!["processes"]))
            .ToList();
        processes.Distinct(StringComparer.Ordinal).Count().ShouldBe(processes.Count);
        var actions = File["actions"]!.AsArray().Select(static a => Str(a!, "id")).ToList();
        actions.Distinct(StringComparer.Ordinal).Count().ShouldBe(actions.Count);
    }

    [Fact]
    public void An_exception_never_repeats_the_standard_combination()
    {
        foreach (var action in File["actions"]!.AsArray())
        {
            var standard = string.Join('+', Strings(action!["keys"]));
            foreach (var exception in action["exceptions"]!.AsArray())
            {
                string.Equals(
                        string.Join('+', Strings(exception!["keys"])),
                        standard,
                        StringComparison.Ordinal
                    )
                    .ShouldBeFalse(Str(action, "id"));
            }
        }
    }

    [Fact]
    public void The_seed_and_the_library_install_each_common_action_with_its_standard_combination()
    {
        var standard = File["actions"]!
            .AsArray()
            .ToDictionary(
                static a => Str(a!, "id"),
                static a => string.Join('+', Strings(a!["keys"])),
                StringComparer.Ordinal
            );
        var seed = CatalogFiles.LoadObject(CatalogFiles.ContentFile("seed.json"));
        var library = CatalogFiles.LoadObject(CatalogFiles.ContentFile("library.json"));
        var items = seed["alwaysVisible"]!
            .AsArray()
            .Concat(seed["general"]!["shortcuts"]!.AsArray())
            .Concat(
                library["sections"]!.AsArray().SelectMany(static s => s!["shortcuts"]!.AsArray())
            );

        var checkedItems = 0;
        foreach (var item in items)
        {
            if (
                standard.TryGetValue(Str(item!, "id"), out var keys)
                && string.Equals(
                    item!["action"]!["type"]!.GetValue<string>(),
                    "tap",
                    StringComparison.Ordinal
                )
            )
            {
                string.Join('+', Strings(item["action"]!["keys"])).ShouldBe(keys, Str(item, "id"));
                checkedItems++;
            }
        }

        checkedItems.ShouldBeGreaterThan(0);
    }

    private static IEnumerable<JsonNode> Exceptions() =>
        File["actions"]!
            .AsArray()
            .SelectMany(static a => a!["exceptions"]!.AsArray())
            .OfType<JsonNode>();

    private static HashSet<string> Ids(string section) =>
        File[section]!
            .AsArray()
            .Select(static n => Str(n!, "id"))
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> Strings(JsonNode? array) =>
        array!.AsArray().Select(static n => n!.GetValue<string>());

    private static string Str(JsonNode node, string name) => node[name]!.GetValue<string>();
}
