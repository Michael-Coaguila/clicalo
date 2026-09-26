using System.Text.Json;
using Json.Schema;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// Loads the JSON Schemas of data/schemas into an isolated registry. Schemas reference each other through their
/// $id (relative $ref), which the registry resolves from the local files, never from the network.
/// </summary>
internal sealed class CatalogSchemas
{
    public const string BaseUri =
        "https://raw.githubusercontent.com/Michael-Coaguila/clicalo/main/data/schemas/";

    private static readonly Lazy<CatalogSchemas> Instance = new(() => new CatalogSchemas());

    private readonly BuildOptions _options;
    private readonly Dictionary<string, JsonSchema> _byFile = new(StringComparer.Ordinal);

    private CatalogSchemas()
    {
        var registry = new SchemaRegistry { Fetch = (uri, _) => Fetch(uri) };
        _options = new BuildOptions { SchemaRegistry = registry };

        // Shared definitions first: every other schema references them.
        foreach (
            var name in new[] { "common.schema.json", "shortcut.schema.json" }
                .Concat(SchemaFileNames())
                .Distinct(StringComparer.Ordinal)
        )
        {
            _ = Get(name);
        }
    }

    public static CatalogSchemas Shared => Instance.Value;

    public static IReadOnlyList<string> SchemaFileNames() =>
        [
            .. Directory
                .GetFiles(CatalogFiles.SchemasDirectory, "*.schema.json")
                .Select(Path.GetFileName)
                .OfType<string>()
                .Order(StringComparer.Ordinal),
        ];

    public JsonSchema Get(string fileName)
    {
        lock (_byFile)
        {
            if (!_byFile.TryGetValue(fileName, out var schema))
            {
                schema = JsonSchema.FromText(
                    File.ReadAllText(Path.Combine(CatalogFiles.SchemasDirectory, fileName)),
                    _options
                );
                _byFile[fileName] = schema;
            }

            return schema;
        }
    }

    /// <summary>Validates a JSON document and returns one readable line per error (empty when valid).</summary>
    public static IReadOnlyList<string> Validate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);
        var results = schema.Evaluate(
            document.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List }
        );
        if (results.IsValid)
        {
            return [];
        }

        var errors = new List<string>();
        foreach (var detail in results.Details ?? [])
        {
            foreach (
                var error in detail.Errors ?? new Dictionary<string, string>(StringComparer.Ordinal)
            )
            {
                errors.Add($"{detail.InstanceLocation} ({detail.EvaluationPath}): {error.Value}");
            }
        }

        return errors.Count == 0 ? ["The document is invalid."] : errors;
    }

    private JsonSchema? Fetch(Uri uri)
    {
        var text = uri.ToString();
        return text.StartsWith(BaseUri, StringComparison.Ordinal)
            ? Get(text[BaseUri.Length..])
            : null;
    }
}
