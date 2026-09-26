using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// Cross-file integrity: every key, icon, category, mouse action and system command referenced by a catalog or
/// by the content exists, and the shipped content is safe and free of repeated combinations.
/// </summary>
public sealed class ContentIntegrityTests
{
    private static KeyCatalog Keys => KeyCatalog.Shared;

    [Fact]
    [Trait("Req", "CAT-004")]
    public void Every_key_used_by_the_content_exists()
    {
        var unknown = ContentCatalog
            .Shortcuts.SelectMany(s => s.AllKeys.Select(k => (s.Where, Key: k)))
            .Where(x => !Keys.ById.ContainsKey(x.Key))
            .Select(x => x.Where + ": " + x.Key);

        unknown.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "CAT-004")]
    public void Every_key_used_by_the_blocked_and_icon_catalogs_exists()
    {
        var chords = BlockedCombos()
            .Select(c => c.Keys)
            .Concat(ComboIcons("es").Concat(ComboIcons("en")).Select(c => c.Keys));

        chords.SelectMany(c => c).Where(k => !Keys.ById.ContainsKey(k)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "CAT-002")]
    public void Every_icon_used_anywhere_exists_in_the_icon_library()
    {
        var icons = IconIds();
        var used = new List<(string Where, string Icon)>();
        used.AddRange(ContentCatalog.Shortcuts.Select(s => (s.Where, s.Icon)));
        used.AddRange(
            ContentCatalog.Templates.Select(t =>
                ("template " + t["id"], t["icon"]!.GetValue<string>())
            )
        );
        used.Add(("seed general", ContentCatalog.Seed["general"]!["icon"]!.GetValue<string>()));
        used.AddRange(
            Items("mouse.json", "actions")
                .Select(a => ("mouse " + a["id"], a["icon"]!.GetValue<string>()))
        );
        used.AddRange(
            Items("system-commands.json", "commands")
                .Select(c => ("system " + c["id"], c["icon"]!.GetValue<string>()))
        );
        used.AddRange(
            ComboIcons("es")
                .Concat(ComboIcons("en"))
                .Select(c => ("combo " + string.Join("+", c.Keys), c.Icon))
        );
        var iconCatalog = CatalogFiles.LoadObject(CatalogFiles.Catalog("icons.json"));
        used.AddRange(Strings(iconCatalog["featured"]!).Select(i => ("featured", i)));
        used.AddRange(Strings(iconCatalog["profileFeatured"]!).Select(i => ("profileFeatured", i)));
        used.AddRange(
            iconCatalog["defaults"]!
                .AsObject()
                .Select(d => ("default " + d.Key, d.Value!.GetValue<string>()))
        );

        used.Where(u => !icons.Contains(u.Icon))
            .Select(u => u.Where + ": " + u.Icon)
            .ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "CAT-002")]
    public void Icons_are_unique_and_searchable_in_both_languages()
    {
        var icons = Items("icons.json", "icons");

        icons.Select(i => i["id"]!.GetValue<string>()).ShouldBeUnique(StringComparer.Ordinal);
        foreach (var icon in icons)
        {
            foreach (var language in new[] { "es", "en" })
            {
                var tags = Strings(icon["tags"]![language]!);
                tags.ShouldNotBeEmpty(icon["id"] + " " + language);
                tags.ShouldAllBe(t => Ordinal.IsLowerCase(t));
            }
        }

        var catalog = CatalogFiles.LoadObject(CatalogFiles.Catalog("icons.json"));
        Strings(catalog["featured"]!)
            .Count.ShouldBe(24, "EDI-004 shows the 24 icons of ICONS first");
        Strings(catalog["profileFeatured"]!)
            .Count.ShouldBe(28, "ATJ-004 offers the current icon plus 28");
    }

    [Fact]
    [Trait("Req", "TEM-003")]
    public void Every_category_used_exists_and_there_are_ten()
    {
        var categories = Items("categories.json", "categories")
            .Select(c => c["id"]!.GetValue<string>())
            .ToList();
        categories.ShouldBeUnique(StringComparer.Ordinal);
        categories.Count.ShouldBe(10);

        var used = ContentCatalog
            .Shortcuts.Select(s => (s.Where, s.Category))
            .Concat(
                ContentCatalog
                    .Templates.Where(t => t["category"] is not null)
                    .Select(t => ("template " + t["id"], t["category"]!.GetValue<string>()))
            );
        used.Where(u => !categories.Contains(u.Item2, StringComparer.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void Mouse_actions_and_speeds_are_consistent()
    {
        var actions = Items("mouse.json", "actions")
            .Select(a => a["id"]!.GetValue<string>())
            .ToList();
        actions.ShouldBeUnique(StringComparer.Ordinal);
        actions.Count.ShouldBe(8, "EJE-009 defines eight mouse actions");

        var mouse = CatalogFiles.LoadObject(CatalogFiles.Catalog("mouse.json"));
        var speeds = Items("mouse.json", "scrollSpeeds")
            .Select(s => s["id"]!.GetValue<string>())
            .ToList();
        speeds.ShouldBe(["slow", "normal", "fast"]);
        speeds.ShouldContain(s => Ordinal.Is(s, mouse["defaultScrollSpeed"]!.GetValue<string>()));

        var used = ContentCatalog
            .Shortcuts.Where(s => Ordinal.Is(s.Type, "mouse"))
            .Select(s => s.Action["mouse"]!.GetValue<string>())
            .Concat(
                ContentCatalog
                    .Shortcuts.SelectMany(s => s.Steps)
                    .Where(s => s["mouse"] is not null)
                    .Select(s => s["mouse"]!.GetValue<string>())
            );
        used.Where(m => !actions.Contains(m, StringComparer.Ordinal)).ShouldBeEmpty();
        ContentCatalog
            .Shortcuts.Where(s => s.Action["speed"] is not null)
            .ShouldAllBe(s => speeds.Contains(s.Action["speed"]!.GetValue<string>()));
    }

    [Fact]
    [Trait("Req", "EJE-016")]
    public void System_commands_used_exist()
    {
        var commands = Items("system-commands.json", "commands")
            .Select(c => c["id"]!.GetValue<string>())
            .ToList();
        commands.ShouldBeUnique(StringComparer.Ordinal);
        commands.ShouldContain(c => Ordinal.Is(c, "lock"));

        var used = ContentCatalog
            .Shortcuts.Where(s => Ordinal.Is(s.Type, "system"))
            .Select(s => s.Action["command"]!.GetValue<string>())
            .Concat(
                BlockedCombos().Where(b => b.Alternative is not null).Select(b => b.Alternative!)
            );
        used.Where(c => !commands.Contains(c, StringComparer.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EDI-013")]
    [Trait("Req", "NFR-020")]
    public void Macro_waits_stay_within_the_named_range_and_step()
    {
        var range = TimingsCatalog.Shared.Entry("Macro", "MacroWaitRange")["durationRange"]!;
        var min = TimingsCatalog.Duration(range["min"]!.GetValue<string>());
        var max = TimingsCatalog.Duration(range["max"]!.GetValue<string>());
        var step = TimingsCatalog.Duration(range["step"]!.GetValue<string>());

        foreach (
            var wait in ContentCatalog
                .Shortcuts.SelectMany(s => s.Steps)
                .Where(s => s["ms"] is not null)
        )
        {
            var value = TimeSpan.FromMilliseconds(wait["ms"]!.GetValue<int>());
            value.ShouldBeInRange(min, max);
            (value.Ticks % step.Ticks).ShouldBe(0);
        }
    }

    [Fact]
    [Trait("Req", "EJE-014")]
    public void Shipped_content_never_uses_a_blocked_combination()
    {
        var blocked = BlockedCombos()
            .Where(b => Ordinal.Is(b.Level, "blocked"))
            .Select(b => Keys.Canonical(b.Keys, genericIsLeft: true))
            .ToHashSet(StringComparer.Ordinal);

        var offending = ContentCatalog
            .Shortcuts.Where(s => s.Keys is not null)
            .Where(s => blocked.Contains(Keys.Canonical(s.Keys!, genericIsLeft: true)))
            .Select(s => s.Where);

        offending.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REP-001")]
    public void Blocked_combinations_are_listed_once_in_canonical_form()
    {
        BlockedCombos()
            .Select(b => Keys.Canonical(b.Keys, genericIsLeft: true))
            .ShouldBeUnique(StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Req", "CAT-003")]
    [Trait("Req", "REP-002")]
    public void Initial_content_creates_no_repeated_combinations()
    {
        var seed = ContentCatalog
            .Lists.Where(l => Ordinal.Is(l.File, "seed.json"))
            .SelectMany(l => l.Shortcuts);

        Repeated(seed).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REP-002")]
    public void No_template_and_no_library_repeats_a_combination()
    {
        foreach (
            var group in ContentCatalog
                .Lists.Where(l => !Ordinal.Is(l.File, "seed.json"))
                .GroupBy(l => l.File, StringComparer.Ordinal)
        )
        {
            Repeated(group.SelectMany(l => l.Shortcuts)).ShouldBeEmpty(group.Key);
        }
    }

    [Fact]
    public void Shortcut_ids_are_unique_within_each_file()
    {
        foreach (var file in ContentCatalog.Lists.GroupBy(l => l.File, StringComparer.Ordinal))
        {
            file.SelectMany(l => l.Shortcuts)
                .Select(s => s.Id)
                .ShouldBeUnique(StringComparer.Ordinal, file.Key);
        }
    }

    [Fact]
    [Trait("Req", "EJE-011")]
    [Trait("Req", "LOG-008")]
    public void Web_targets_are_http_or_https_and_no_app_is_shipped()
    {
        foreach (var web in ContentCatalog.Shortcuts.Where(s => Ordinal.Is(s.Type, "web")))
        {
            var uri = new Uri(web.Action["url"]!.GetValue<string>());
            uri.Scheme.ShouldBeOneOf(Uri.UriSchemeHttps, Uri.UriSchemeHttp);
        }

        ContentCatalog.Shortcuts.ShouldNotContain(
            s => s.Type == "app",
            "shipped content never launches programs"
        );
    }

    [Fact]
    [Trait("Req", "LOG-005")]
    public void Library_texts_are_placeholders_without_personal_data()
    {
        var texts = ContentCatalog.Shortcuts.Where(s => Ordinal.Is(s.Type, "text")).ToList();

        texts.ShouldNotBeEmpty();
        foreach (var text in texts)
        {
            text.Action["text"]!.GetValue<string>().ShouldBeEmpty(text.Where);
            text.Action["hint"].ShouldNotBeNull(text.Where);
        }
    }

    [Fact]
    [Trait("Req", "CAT-006")]
    [Trait("Req", "PER-002")]
    public void Templates_are_named_after_their_file_and_bind_distinct_processes()
    {
        foreach (var path in CatalogFiles.TemplateFiles())
        {
            CatalogFiles.LoadObject(path)["id"]!
                .GetValue<string>()
                .ShouldBe(Path.GetFileNameWithoutExtension(path));
        }

        var processes = ContentCatalog.Templates.SelectMany(t => Strings(t["processes"]!)).ToList();
        processes.ShouldBeUnique(
            StringComparer.OrdinalIgnoreCase,
            "a process belongs to one profile (ATJ-007)"
        );
        processes.ShouldAllBe(p => Ordinal.IsLowerCase(p));
    }

    [Fact]
    [Trait("Req", "CAT-005")]
    public void Variants_target_a_reviewed_programs_language_other_than_Spanish()
    {
        foreach (var template in ContentCatalog.Templates)
        {
            var languages = Strings(template["appsLanguages"]!);
            languages.ShouldContain(l => Ordinal.Is(l, "es"));
            var id = template["id"]!.GetValue<string>();
            foreach (
                var shortcut in ContentCatalog
                    .Lists.Single(l => Ordinal.Is(l.Name, "template:" + id))
                    .Shortcuts
            )
            {
                shortcut.Variants.Keys.ShouldAllBe(language =>
                    !Ordinal.Is(language, "es")
                    && languages.Contains(language, StringComparer.Ordinal)
                );
                shortcut.Variants.Values.ShouldAllBe(keys => !keys.SequenceEqual(shortcut.Keys!));
            }
        }

        foreach (var office in new[] { "word", "excel", "ppt", "outlook" })
        {
            var template = ContentCatalog.Templates.Single(t =>
                Ordinal.Is(t["id"]!.GetValue<string>(), office)
            );
            Strings(template["appsLanguages"]!).ShouldContain(l => Ordinal.Is(l, "en"), office);
        }

        ContentCatalog
            .Shortcuts.Where(s => !Ordinal.Is(s.Type, "tap"))
            .ShouldAllBe(s => s.Action["variants"] == null);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "EDI-005")]
    [Trait("Req", "CAT-005")]
    public void Combination_icons_are_per_programs_language_without_repeats(string language)
    {
        var file = CatalogFiles.LoadObject(CatalogFiles.Catalog($"combo-icons.{language}.json"));
        file["appsLanguage"]!.GetValue<string>().ShouldBe(language);

        ComboIcons(language)
            .Select(c => Keys.Canonical(c.Keys))
            .ShouldBeUnique(StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Req", "CAT-005")]
    public void Office_bold_follows_the_programs_language()
    {
        ComboIcons("es")
            .Single(c => Ordinal.Is(c.Icon, "format_bold"))
            .Keys.ShouldBe(["ctrl", "n"]);
        ComboIcons("en")
            .Single(c => Ordinal.Is(c.Icon, "format_bold"))
            .Keys.ShouldBe(["ctrl", "b"]);
    }

    private static IEnumerable<string> Repeated(IEnumerable<ContentShortcut> shortcuts) =>
        shortcuts
            .Where(s => s.Keys is not null)
            .GroupBy(s => Keys.Canonical(s.Keys!), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key + ": " + string.Join(", ", g.Select(s => s.Where)));

    private static HashSet<string> IconIds() =>
        Items("icons.json", "icons")
            .Select(i => i["id"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<JsonObject> Items(string catalog, string member) =>
        [
            .. CatalogFiles.LoadObject(CatalogFiles.Catalog(catalog))[member]!
                .AsArray()
                .Select(n => n!.AsObject()),
        ];

    private static IReadOnlyList<(
        IReadOnlyList<string> Keys,
        string Level,
        string? Alternative
    )> BlockedCombos() =>
        [
            .. Items("blocked-combos.json", "combos")
                .Select(c =>
                    (
                        Strings(c["keys"]!),
                        c["level"]!.GetValue<string>(),
                        c["alternative"]?["systemCommand"]?.GetValue<string>()
                    )
                ),
        ];

    private static IReadOnlyList<(IReadOnlyList<string> Keys, string Icon)> ComboIcons(
        string language
    ) =>
        [
            .. Items($"combo-icons.{language}.json", "entries")
                .Select(e => (Strings(e["keys"]!), e["icon"]!.GetValue<string>())),
        ];

    private static IReadOnlyList<string> Strings(JsonNode node) =>
        ContentShortcut.Strings(node.AsArray());
}
