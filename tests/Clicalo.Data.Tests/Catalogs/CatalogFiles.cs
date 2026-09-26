using System.Text.Json;
using System.Text.Json.Nodes;
using Clicalo.TestKit;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>Locations and strict loaders of the catalog, content, schema and handoff files.</summary>
internal static class CatalogFiles
{
    private static readonly JsonDocumentOptions Strict = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        AllowDuplicateProperties = false,
    };

    public static string CatalogsDirectory => Path.Combine(RepoPaths.Data, "catalogs");

    public static string ContentDirectory => Path.Combine(RepoPaths.Data, "content");

    public static string TemplatesDirectory => Path.Combine(ContentDirectory, "templates");

    public static string SchemasDirectory => Path.Combine(RepoPaths.Data, "schemas");

    public static string HandoffSeedPath =>
        Path.Combine(RepoPaths.Handoff, "data", "seed-and-catalogs.json");

    public static string Catalog(string fileName) => Path.Combine(CatalogsDirectory, fileName);

    public static string ContentFile(string fileName) => Path.Combine(ContentDirectory, fileName);

    /// <summary>Every data file validated by a schema: catalogs, content and templates.</summary>
    public static IReadOnlyList<string> DataFiles() =>
        [
            .. Directory.GetFiles(CatalogsDirectory, "*.json"),
            .. Directory.GetFiles(ContentDirectory, "*.json"),
            .. Directory.GetFiles(TemplatesDirectory, "*.json"),
        ];

    public static IReadOnlyList<string> TemplateFiles() =>
        [
            .. Directory
                .GetFiles(TemplatesDirectory, "*.json")
                .OrderBy(p => p, StringComparer.Ordinal),
        ];

    public static JsonNode Parse(string path) =>
        JsonNode.Parse(File.ReadAllText(path), documentOptions: Strict)
        ?? throw new InvalidDataException(path + " is empty.");

    public static JsonObject LoadObject(string path) => Parse(path).AsObject();

    /// <summary>Repository-relative path with forward slashes, for readable test names and messages.</summary>
    public static string Relative(string path) =>
        Path.GetRelativePath(RepoPaths.Root, path).Replace('\\', '/');
}
