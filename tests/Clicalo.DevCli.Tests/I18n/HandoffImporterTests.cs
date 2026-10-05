using Clicalo.DevCli.I18n;

namespace Clicalo.DevCli.Tests.I18n;

/// <summary>The reviewed conversion from the design handoff to data/i18n (handoff-import.json).</summary>
public sealed class HandoffImporterTests
{
    private const string BaseRecipe = """
        {
          "source": "docs/design/handoff/data",
          "languages": ["es", "en"],
          "placeholders": { "n": "count", "a": "app" },
          "keys": {
            "sugLine": {
              "plural": {
                "es": { "one": "{app}: {count} atajo" },
                "en": { "one": "{app}: {count} shortcut" }
              }
            }
          },
          "added": {
            "catEdit": { "after": "title", "text": { "es": "Edición", "en": "Editing" } }
          }
        }
        """;

    private static readonly Dictionary<
        string,
        IReadOnlyList<KeyValuePair<string, string>>
    > Handoff = new(StringComparer.Ordinal)
    {
        ["es"] = [new("title", "Clícalo"), new("sugLine", "{a}: {n} atajos")],
        ["en"] = [new("title", "Clícalo"), new("sugLine", "{a}: {n} shortcuts")],
    };

    [Fact]
    public void Markers_get_their_names_plurals_get_forms_and_added_texts_follow_their_key()
    {
        var import = HandoffImporter.Run(HandoffRecipe.Parse(BaseRecipe), Handoff);

        import.Errors.ShouldBeEmpty();
        import.HandoffKeyCount.ShouldBe(2);
        import
            .Entries["es"]
            .ShouldBe([
                new("title", "Clícalo"),
                new("catEdit", "Edición"),
                new("sugLine_one", "{app}: {count} atajo"),
                new("sugLine_other", "{app}: {count} atajos"),
            ]);
        import.Entries["en"][1].ShouldBe(new KeyValuePair<string, string>("catEdit", "Editing"));
    }

    [Fact]
    public void An_unknown_marker_is_an_error()
    {
        var handoff = new Dictionary<string, IReadOnlyList<KeyValuePair<string, string>>>(
            StringComparer.Ordinal
        )
        {
            ["es"] = [new("title", "Clícalo {q}"), new("sugLine", "{a}: {n} atajos")],
            ["en"] = [new("title", "Clícalo {q}"), new("sugLine", "{a}: {n} shortcuts")],
        };

        var import = HandoffImporter.Run(HandoffRecipe.Parse(BaseRecipe), handoff);

        import.Errors.ShouldContain(e =>
            e.StartsWith("Unknown marker {q} in 'title'", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void An_added_key_whose_anchor_does_not_exist_is_an_error()
    {
        var recipe = BaseRecipe.Replace(
            "\"after\": \"title\"",
            "\"after\": \"nowhere\"",
            StringComparison.Ordinal
        );

        var import = HandoffImporter.Run(HandoffRecipe.Parse(recipe), Handoff);

        import.Errors.ShouldBe(["added.catEdit.after ('nowhere') is not a key of the output."]);
    }

    [Fact]
    public void An_added_key_missing_a_language_is_an_error()
    {
        var recipe = BaseRecipe.Replace(
            ", \"en\": \"Editing\"",
            string.Empty,
            StringComparison.Ordinal
        );

        var import = HandoffImporter.Run(HandoffRecipe.Parse(recipe), Handoff);

        import.Errors.ShouldBe(["added.catEdit.text lacks the language 'en'."]);
    }

    [Theory]
    [InlineData(
        "\"text\": { \"es\": \"A\", \"en\": \"B\" }, \"plural\": { \"es\": { \"other\": \"A\" }, \"en\": { \"other\": \"B\" } }"
    )]
    [InlineData("\"notes\": \"neither text nor plural\"")]
    public void An_added_key_needs_exactly_one_of_text_or_plural(string body)
    {
        var recipe = BaseRecipe.Replace(
            "\"text\": { \"es\": \"Edición\", \"en\": \"Editing\" }",
            body,
            StringComparison.Ordinal
        );

        Should
            .Throw<InvalidDataException>(() => HandoffRecipe.Parse(recipe))
            .Message.ShouldContain("added.catEdit needs exactly one of 'text' or 'plural'.");
    }

    [Fact]
    public void An_unknown_property_in_the_recipe_is_an_error()
    {
        var recipe = BaseRecipe.Replace(
            "\"after\": \"title\"",
            "\"after\": \"title\", \"afer\": \"x\"",
            StringComparison.Ordinal
        );

        Should
            .Throw<InvalidDataException>(() => HandoffRecipe.Parse(recipe))
            .Message.ShouldContain("Unknown property 'afer' in added.catEdit.");
    }

    [Fact]
    public void A_retired_key_is_not_imported()
    {
        var recipe = BaseRecipe
            .Replace(
                "\"added\": {",
                "\"retired\": { \"title\": { \"notes\": \"Decision of the user.\" } }, \"added\": {",
                StringComparison.Ordinal
            )
            .Replace("\"after\": \"title\"", "\"after\": \"sugLine\"", StringComparison.Ordinal);

        var import = HandoffImporter.Run(HandoffRecipe.Parse(recipe), Handoff);

        import.Errors.ShouldBeEmpty();
        import.HandoffKeyCount.ShouldBe(2);
        import
            .Entries["es"]
            .ShouldBe([
                new("sugLine_one", "{app}: {count} atajo"),
                new("sugLine_other", "{app}: {count} atajos"),
                new("catEdit", "Edición"),
            ]);
    }

    [Fact]
    public void An_added_key_anchored_to_a_retired_key_is_an_error()
    {
        var recipe = BaseRecipe.Replace(
            "\"added\": {",
            "\"retired\": { \"title\": { \"notes\": \"Decision of the user.\" } }, \"added\": {",
            StringComparison.Ordinal
        );

        var import = HandoffImporter.Run(HandoffRecipe.Parse(recipe), Handoff);

        import.Errors.ShouldBe(["added.catEdit.after ('title') is not a key of the output."]);
    }

    [Theory]
    [InlineData("missing", "retired.missing is not a key of the handoff.")]
    [InlineData("sugLine", "keys.sugLine converts a retired key; remove the rule.")]
    public void A_retired_key_must_exist_and_have_no_rule(string key, string error)
    {
        var recipe = BaseRecipe.Replace(
            "\"added\": {",
            "\"retired\": { \""
                + key
                + "\": { \"notes\": \"Decision of the user.\" } }, \"added\": {",
            StringComparison.Ordinal
        );

        var import = HandoffImporter.Run(HandoffRecipe.Parse(recipe), Handoff);

        import.Errors.ShouldContain(e => string.Equals(e, error, StringComparison.Ordinal));
    }

    [Fact]
    public void A_retired_key_needs_its_reason()
    {
        var recipe = BaseRecipe.Replace(
            "\"added\": {",
            "\"retired\": { \"title\": {} }, \"added\": {",
            StringComparison.Ordinal
        );

        Should
            .Throw<InvalidDataException>(() => HandoffRecipe.Parse(recipe))
            .Message.ShouldContain("retired.title needs the property 'notes'.");
    }
}
