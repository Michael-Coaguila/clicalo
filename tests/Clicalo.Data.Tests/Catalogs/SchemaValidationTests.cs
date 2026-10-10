using Json.Schema;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>Every catalog and content file validates against its JSON Schema 2020-12 (CAT-001, CAT-004, LOG-006).</summary>
public sealed class SchemaValidationTests
{
    private const string MetaSchema = "https://json-schema.org/draft/2020-12/schema";

    public static TheoryData<string> DataFiles() =>
        [.. CatalogFiles.DataFiles().Select(CatalogFiles.Relative)];

    public static TheoryData<string> SchemaFiles() => [.. CatalogSchemas.SchemaFileNames()];

    /// <summary>
    /// The persisted formats (ADR-0007, ADR-0018, ADR-0028): no catalog or content file uses them; the immutable 1.0 and
    /// 1.1 fixtures of the Infrastructure tests (blueprint §6.6) do.
    /// </summary>
    public static TheoryData<string, string, string> PersistedFixtures() =>
        new()
        {
            { "1.0", "document.json", "document.schema.json" },
            { "1.0", "usage.json", "usage.schema.json" },
            { "1.0", "ai-template.json", "ai-template.v1.schema.json" },
            { "1.1", "document.json", "document.schema.json" },
        };

    [Theory]
    [MemberData(nameof(DataFiles))]
    [Trait("Req", "CAT-001")]
    [Trait("Req", "CAT-004")]
    public void Data_file_is_valid_against_the_schema_it_declares(string relativePath)
    {
        var path = Path.Combine(Clicalo.TestKit.RepoPaths.Root, relativePath);
        var schemaFile = DeclaredSchema(path);

        var errors = CatalogSchemas.Validate(
            CatalogSchemas.Shared.Get(schemaFile),
            File.ReadAllText(path)
        );

        errors.ShouldBeEmpty(relativePath + " must match " + schemaFile);
    }

    [Theory]
    [MemberData(nameof(DataFiles))]
    public void Data_file_declares_an_existing_schema_by_relative_path(string relativePath)
    {
        var path = Path.Combine(Clicalo.TestKit.RepoPaths.Root, relativePath);
        var declared = CatalogFiles.LoadObject(path)["$schema"]?.GetValue<string>();

        declared.ShouldNotBeNull(
            relativePath + " must declare $schema so editors and tests agree on its schema"
        );
        var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, declared));
        Path.GetDirectoryName(resolved).ShouldBe(Path.GetFullPath(CatalogFiles.SchemasDirectory));
        File.Exists(resolved).ShouldBeTrue(declared + " does not exist");
    }

    [Theory]
    [MemberData(nameof(SchemaFiles))]
    public void Schema_is_draft_2020_12_with_an_id_that_matches_its_file(string schemaFile)
    {
        var schema = CatalogFiles.LoadObject(
            Path.Combine(CatalogFiles.SchemasDirectory, schemaFile)
        );

        schema["$schema"]?.GetValue<string>().ShouldBe(MetaSchema);
        schema["$id"]?.GetValue<string>().ShouldBe(CatalogSchemas.BaseUri + schemaFile);
        CatalogSchemas.Shared.Get(schemaFile).ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(SchemaFiles))]
    public void Schema_is_itself_valid_against_the_2020_12_meta_schema(string schemaFile)
    {
        var metaSchema = SchemaRegistry.Global.Get(new Uri(MetaSchema)) as JsonSchema;
        metaSchema.ShouldNotBeNull();

        var errors = CatalogSchemas.Validate(
            metaSchema,
            File.ReadAllText(Path.Combine(CatalogFiles.SchemasDirectory, schemaFile))
        );

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Every_schema_is_used_by_a_data_file_or_referenced_by_another_schema()
    {
        var used = CatalogFiles
            .DataFiles()
            .Select(DeclaredSchema)
            .Concat(["common.schema.json", "shortcut.schema.json"])
            .Concat(PersistedFixtures().Select(row => row.Data.Item3))
            .ToHashSet(StringComparer.Ordinal);

        CatalogSchemas.SchemaFileNames().Where(name => !used.Contains(name)).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(PersistedFixtures))]
    [Trait("Req", "DAT-001")]
    public void The_persisted_fixtures_are_valid_against_their_format(
        string version,
        string fixture,
        string schemaFile
    )
    {
        var path = Clicalo.TestKit.RepoPaths.Combine(
            "tests",
            "Clicalo.Infrastructure.Tests",
            "Fixtures",
            "schema",
            version,
            fixture
        );

        CatalogSchemas
            .Validate(CatalogSchemas.Shared.Get(schemaFile), File.ReadAllText(path))
            .ShouldBeEmpty(fixture + " must match " + schemaFile);
    }

    [Theory]
    [Trait("Req", "NFR-020")]
    [InlineData(
        """{ "duration": 600, "description": "x", "req": ["NFR-020"] }""",
        "a time without unit"
    )]
    [InlineData(
        """{ "duration": "-5ms", "description": "x", "req": ["NFR-020"] }""",
        "a negative time"
    )]
    [InlineData(
        """{ "duration": "5ms", "px": 3, "description": "x", "req": ["NFR-020"] }""",
        "two value kinds"
    )]
    [InlineData("""{ "duration": "5ms", "description": "x" }""", "no requirement nor source")]
    public void Timings_schema_rejects_malformed_entries(string entry, string because)
    {
        var document =
            $$"""{ "catalogVersion": 1, "groups": { "Touch": { "description": "x", "entries": { "LongPress": {{entry}} } } } }""";

        CatalogSchemas
            .Validate(CatalogSchemas.Shared.Get("timings.schema.json"), document)
            .ShouldNotBeEmpty(because);
    }

    [Theory]
    [Trait("Req", "CAT-004")]
    [InlineData(
        """{ "type": "tap", "keys": ["Ctrl", "C"] }""",
        "display labels instead of key ids"
    )]
    [InlineData("""{ "type": "tap", "keys": [] }""", "an empty combination")]
    [InlineData(
        """{ "type": "toggle", "keys": ["shift"], "mouse": "drag" }""",
        "a mouse action mixed into a key action"
    )]
    [InlineData(
        """{ "type": "macro", "steps": [{ "keys": ["f12"] }] }""",
        "a macro step without kind"
    )]
    [InlineData(
        """{ "type": "web", "url": "file:///C:/Windows/System32/cmd.exe" }""",
        "a non-http web target"
    )]
    [InlineData("""{ "type": "text" }""", "a text action without text")]
    public void Shortcut_schema_rejects_invalid_actions(string action, string because)
    {
        var document = $$"""
            { "catalogVersion": 1, "sections": [ { "id": "edit", "labelKey": "lcEdit", "shortcuts": [
              { "id": "x", "name": { "es": "X", "en": "X" }, "icon": "bolt", "category": "edit", "action": {{action}} } ] } ] }
            """;

        CatalogSchemas
            .Validate(CatalogSchemas.Shared.Get("library.schema.json"), document)
            .ShouldNotBeEmpty(because);
    }

    [Theory]
    [InlineData("Ctrl", "an upper-case id")]
    [InlineData("char:ab", "a character key with two characters")]
    [InlineData("num add", "a space")]
    public void Key_schema_rejects_non_canonical_ids(string id, string because)
    {
        var document = $$"""
            { "catalogVersion": 1, "groups": [ { "id": "mods", "labelKey": "kgMods" } ],
              "keys": [ { "id": "{{id}}", "codeName": "Ctrl", "group": "mods", "label": { "es": "Ctrl", "en": "Ctrl" } } ] }
            """;

        CatalogSchemas
            .Validate(CatalogSchemas.Shared.Get("keys.schema.json"), document)
            .ShouldNotBeEmpty(because);
    }

    private static string DeclaredSchema(string path)
    {
        var declared = CatalogFiles.LoadObject(path)["$schema"]?.GetValue<string>() ?? string.Empty;
        return Path.GetFileName(declared);
    }
}
