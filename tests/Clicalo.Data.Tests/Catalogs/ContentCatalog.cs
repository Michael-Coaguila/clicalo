using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>Test view of the content files: library, seed and templates, as lists of shortcuts.</summary>
internal static class ContentCatalog
{
    private static readonly Lazy<IReadOnlyList<ShortcutList>> AllLists = new(LoadLists);

    /// <summary>Every list of shortcuts: seed rows, library sections and templates.</summary>
    public static IReadOnlyList<ShortcutList> Lists => AllLists.Value;

    public static IEnumerable<ContentShortcut> Shortcuts => Lists.SelectMany(l => l.Shortcuts);

    public static JsonObject Library =>
        CatalogFiles.LoadObject(CatalogFiles.ContentFile("library.json"));

    public static JsonObject Seed => CatalogFiles.LoadObject(CatalogFiles.ContentFile("seed.json"));

    public static IReadOnlyList<JsonObject> Templates =>
        [.. CatalogFiles.TemplateFiles().Select(CatalogFiles.LoadObject)];

    private static List<ShortcutList> LoadLists()
    {
        var lists = new List<ShortcutList>();
        var seed = Seed;
        lists.Add(Read("seed.json", "seed:alwaysVisible", seed["alwaysVisible"]!.AsArray()));
        lists.Add(Read("seed.json", "seed:general", seed["general"]!["shortcuts"]!.AsArray()));
        foreach (var section in Library["sections"]!.AsArray())
        {
            lists.Add(
                Read(
                    "library.json",
                    "library:" + section!["id"]!.GetValue<string>(),
                    section["shortcuts"]!.AsArray()
                )
            );
        }

        foreach (var template in Templates)
        {
            var id = template["id"]!.GetValue<string>();
            lists.Add(
                Read(
                    "templates/" + id + ".json",
                    "template:" + id,
                    template["shortcuts"]!.AsArray()
                )
            );
        }

        return lists;
    }

    private static ShortcutList Read(string file, string name, JsonArray shortcuts) =>
        new(file, name, [.. shortcuts.Select(s => new ContentShortcut(file, name, s!.AsObject()))]);
}

internal sealed record ShortcutList(
    string File,
    string Name,
    IReadOnlyList<ContentShortcut> Shortcuts
);

/// <summary>A shortcut of the content, with helpers over its action.</summary>
internal sealed record ContentShortcut(string File, string List, JsonObject Node)
{
    public string Id => Node["id"]!.GetValue<string>();

    public string NameEs => Node["name"]!["es"]!.GetValue<string>();

    public string NameEn => Node["name"]!["en"]!.GetValue<string>();

    public string Icon => Node["icon"]!.GetValue<string>();

    public string Category => Node["category"]!.GetValue<string>();

    public JsonObject Action => Node["action"]!.AsObject();

    public string Type => Action["type"]!.GetValue<string>();

    public bool Confirm => Node["confirm"]?.GetValue<bool>() ?? false;

    /// <summary>The combination for key actions (tap, hold, toggle); null otherwise.</summary>
    public IReadOnlyList<string>? Keys => Action["keys"] is JsonArray keys ? Strings(keys) : null;

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Variants =>
        Action["variants"] is JsonObject variants
            ? variants.ToDictionary(
                v => v.Key,
                v => Strings(v.Value!.AsArray()),
                StringComparer.Ordinal
            )
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public IReadOnlyList<JsonObject> Steps =>
        Action["steps"] is JsonArray steps ? [.. steps.Select(s => s!.AsObject())] : [];

    /// <summary>Every key id the shortcut can press: its combination, variants and macro steps.</summary>
    public IEnumerable<string> AllKeys =>
        (Keys ?? [])
            .Concat(Variants.Values.SelectMany(v => v))
            .Concat(
                Steps
                    .Where(s => s["keys"] is not null)
                    .SelectMany(s => Strings(s["keys"]!.AsArray()))
            );

    public string Where => File + " › " + List + " › " + Id;

    public static IReadOnlyList<string> Strings(JsonArray array) =>
        [.. array.Select(k => k!.GetValue<string>())];
}
