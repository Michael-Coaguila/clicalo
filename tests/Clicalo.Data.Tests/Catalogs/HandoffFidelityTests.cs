using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// The catalogs keep everything the design handoff (docs/design/handoff/data/seed-and-catalogs.json) defines.
/// Every difference is a documented decision listed here, so a new difference fails until it is justified.
/// </summary>
public sealed class HandoffFidelityTests
{
    /// <summary>Handoff key group → catalog group.</summary>
    public static TheoryData<string, string> KeyGroups() =>
        new()
        {
            { "MODS", "mods" },
            { "mods2", "sides" },
            { "letters", "letters" },
            { "nums", "nums" },
            { "fn", "fn" },
            { "special", "special" },
            { "numpad", "numpad" },
            { "media", "media" },
        };

    /// <summary>Keys added to a group on top of the handoff, with the requirement that asks for them.</summary>
    private static readonly Dictionary<string, string[]> AddedKeys = new(StringComparer.Ordinal)
    {
        // Catalog §7.3: v1 «winright» needs a right Windows key.
        ["sides"] = ["rwin"],
        // MIG-005 and EDI-008: ` \ [ ] ' # used by v1; CAT-004: «%» of the Excel template.
        ["nums"] = ["char:`", "char:\\", "char:[", "char:]", "char:'", "char:#", "char:%"],
    };

    /// <summary>Handoff key labels that are not keys: brightness has no virtual key (EJE-016).</summary>
    private static readonly Dictionary<string, string> KeysBecameSystemCommands = new(
        StringComparer.Ordinal
    )
    {
        ["Brillo +"] = "brightness.up",
        ["Brillo −"] = "brightness.down",
    };

    /// <summary>Shortcuts whose action was changed by a documented decision; the value is the catalog action.</summary>
    private static readonly Dictionary<(string Container, string Id), string> ChangedActions = new()
    {
        // CAT-003, PQ-04: Win+H toggles dictation by itself, so it is a tap.
        [("always", "dict")] = """{ "type": "tap", "keys": ["win", "h"] }""",
        [("library", "l_dict")] = """{ "type": "tap", "keys": ["win", "h"] }""",
        // PQ-04: Alt+A toggles the Zoom microphone by itself.
        [("zoom", "z1")] = """{ "type": "tap", "keys": ["alt", "a"] }""",
        // CAT-004: «Clic izq.» is the mouse drag action, a latch of the left button (EJE-007).
        [("general", "drag")] = """{ "type": "mouse", "mouse": "drag" }""",
        [("library", "l_drag")] = """{ "type": "mouse", "mouse": "drag" }""",
        // EJE-009: scrolling repeats while held by itself; the action kind is Mouse.
        [("library", "l_su")] = """{ "type": "mouse", "mouse": "sup" }""",
        [("library", "l_sd")] = """{ "type": "mouse", "mouse": "sdn" }""",
        // CAT-003, EJE-014, EJE-016: Win+L is blocked; the library offers the system command.
        [("library", "l_lock")] = """{ "type": "system", "command": "lock" }""",
        // LOG-005, DIS-81: example texts are placeholders, not the author's data.
        [("library", "l_mail")] =
            """{ "type": "text", "text": "", "hint": { "es": "nombre@correo.com", "en": "name@email.com" } }""",
        [("library", "l_sign")] =
            """{ "type": "text", "text": "", "hint": { "es": "Saludos cordiales,\nTu nombre", "en": "Best regards,\nYour name" } }""",
        [("library", "l_addr")] =
            """{ "type": "text", "text": "", "hint": { "es": "Calle, número y ciudad", "en": "Street, number and city" } }""",
        // EJE-011: only http and https; the scheme is explicit.
        [("browser", "mail")] = """{ "type": "web", "url": "https://mail.google.com" }""",
        // CAT-005: English variants of the Office templates.
        [("word", "bold")] =
            """{ "type": "tap", "keys": ["ctrl", "n"], "variants": { "en": ["ctrl", "b"] } }""",
        [("word", "italic")] =
            """{ "type": "tap", "keys": ["ctrl", "k"], "variants": { "en": ["ctrl", "i"] } }""",
        [("word", "under")] =
            """{ "type": "tap", "keys": ["ctrl", "s"], "variants": { "en": ["ctrl", "u"] } }""",
        [("word", "replace")] =
            """{ "type": "tap", "keys": ["ctrl", "l"], "variants": { "en": ["ctrl", "h"] } }""",
        [("outlook", "o1")] =
            """{ "type": "tap", "keys": ["ctrl", "u"], "variants": { "en": ["ctrl", "shift", "m"] } }""",
    };

    /// <summary>Handoff shortcuts intentionally left out.</summary>
    private static readonly HashSet<(string Container, string Id)> RemovedShortcuts =
    [
        // CAT-003: the initial content must not create repeated combinations (Copy twice in Browser).
        ("browser", "copyb"),
    ];

    /// <summary>Profiles of the handoff seed that became installable templates (CAT-003, PQ-44).</summary>
    private static readonly Dictionary<string, string> ProfilesBecameTemplates = new(
        StringComparer.Ordinal
    )
    {
        ["word"] = "word",
        ["browser"] = "browser",
        ["code"] = "vscode",
    };

    private static readonly Lazy<JsonObject> Handoff = new(() =>
        CatalogFiles.LoadObject(CatalogFiles.HandoffSeedPath)
    );

    private static KeyCatalog Keys => KeyCatalog.Shared;

    [Theory]
    [MemberData(nameof(KeyGroups))]
    [Trait("Req", "CAT-001")]
    [Trait("Req", "EDI-008")]
    public void Every_handoff_key_is_in_its_group_in_the_same_order(
        string handoffGroup,
        string group
    )
    {
        var labels = Ordinal.Is(handoffGroup, "MODS")
            ? Strings(Handoff.Value["MODS"]!)
            : Strings(Handoff.Value["KEYG"]![handoffGroup]!);
        var mapped = new List<string>();
        foreach (var label in labels.Where(l => !KeysBecameSystemCommands.ContainsKey(l)))
        {
            var key = Keys.Resolve(label);
            key.ShouldNotBeNull($"«{label}» of {handoffGroup} has no key");
            key.Group.ShouldBe(group, label);
            mapped.Add(key.Id);
        }

        var groupKeys = Keys.Keys.Where(k => Ordinal.Is(k.Group, group)).Select(k => k.Id).ToList();
        groupKeys
            .Where(id => mapped.Contains(id, StringComparer.Ordinal))
            .ShouldBe(mapped, "same order as the handoff");
        groupKeys
            .Where(id => !mapped.Contains(id, StringComparer.Ordinal))
            .ShouldBe(AddedKeys.GetValueOrDefault(group) ?? []);
    }

    [Fact]
    [Trait("Req", "EJE-016")]
    public void Brightness_keys_became_system_commands()
    {
        var commands = CatalogFiles.LoadObject(CatalogFiles.Catalog("system-commands.json"))[
            "commands"
        ]!
            .AsArray()
            .Select(c => c!["id"]!.GetValue<string>());

        commands.ShouldBe(["lock", .. KeysBecameSystemCommands.Values]);
        KeysBecameSystemCommands.Keys.ShouldAllBe(label =>
            KeyCatalog.Shared.Resolve(label) == null
        );
    }

    [Fact]
    [Trait("Req", "CAT-002")]
    public void Icon_library_keeps_the_handoff_order_and_every_search_word()
    {
        var handoff = Handoff.Value["ICONLIB"]!
            .AsArray()
            .Select(i => (Id: i![0]!.GetValue<string>(), Tags: i[1]!.GetValue<string>()))
            .ToList();
        var catalog = Icons();

        catalog
            .Take(handoff.Count)
            .Select(i => i.Id)
            .ShouldBe(
                handoff.Select(i => i.Id),
                "suggestIcons walks the library in order (EDI-005)"
            );
        foreach (var (id, tags) in handoff)
        {
            var words = catalog
                .Single(i => Ordinal.Is(i.Id, id))
                .Words.Select(Fold)
                .ToHashSet(StringComparer.Ordinal);
            tags.Split(' ').Select(Fold).Where(w => !words.Contains(w)).ShouldBeEmpty(id);
        }
    }

    [Fact]
    [Trait("Req", "CAT-002")]
    public void Icon_library_covers_the_32_icons_the_handoff_used_without_listing()
    {
        var listed = Handoff.Value["ICONLIB"]!
            .AsArray()
            .Select(i => i![0]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var used = HandoffIcons().Where(i => !listed.Contains(i)).ToHashSet(StringComparer.Ordinal);
        var catalog = Icons().Select(i => i.Id).ToHashSet(StringComparer.Ordinal);

        used.Count.ShouldBe(32);
        used.Where(i => !catalog.Contains(i)).ShouldBeEmpty();
        Strings(CatalogFiles.LoadObject(CatalogFiles.Catalog("icons.json"))["featured"]!)
            .ShouldBe(Strings(Handoff.Value["ICONS"]!));
    }

    [Fact]
    [Trait("Req", "EDI-005")]
    [Trait("Req", "CAT-005")]
    public void Every_combination_icon_of_the_handoff_is_kept_in_a_programs_language()
    {
        var maps = new[] { "es", "en" }
            .SelectMany(language =>
                CatalogFiles.LoadObject(CatalogFiles.Catalog($"combo-icons.{language}.json"))[
                    "entries"
                ]!
                    .AsArray()
                    .Select(e =>
                        (
                            Combo: Keys.Canonical(Strings(e!["keys"]!)),
                            Icon: e["icon"]!.GetValue<string>()
                        )
                    )
            )
            .ToList();

        foreach (var entry in Handoff.Value["KEYICON"]!.AsObject())
        {
            var combo = Keys.Canonical(entry.Key.Split('+').Select(s => Keys.Resolve(s)!.Id));
            maps.ShouldContain(
                m =>
                    Ordinal.Is(m.Combo, combo)
                    && Ordinal.Is(m.Icon, entry.Value!.GetValue<string>()),
                entry.Key
            );
        }
    }

    [Fact]
    [Trait("Req", "EJE-014")]
    [Trait("Req", "REP-001")]
    public void Blocked_combinations_match_the_handoff_with_aliases_collapsed()
    {
        var expected = Handoff.Value["BLOCKED"]!
            .AsObject()
            .Select(b =>
                (
                    Combo: Keys.Canonical(
                        b.Key.Split('+').Select(s => Keys.Resolve(s)!.Id),
                        ignoreSides: true
                    ),
                    Level: Ordinal.Is(b.Value!.GetValue<string>(), "b") ? "blocked" : "special"
                )
            )
            .Distinct()
            .OrderBy(x => x.Combo, StringComparer.Ordinal)
            .ToList();

        var actual = CatalogFiles.LoadObject(CatalogFiles.Catalog("blocked-combos.json"))["combos"]!
            .AsArray()
            .Select(c =>
                (
                    Combo: Keys.Canonical(Strings(c!["keys"]!), ignoreSides: true),
                    Level: c["level"]!.GetValue<string>()
                )
            )
            .OrderBy(x => x.Combo, StringComparer.Ordinal)
            .ToList();

        actual.ShouldBe(expected);
    }

    [Fact]
    [Trait("Req", "TAC-001")]
    public void Touch_presets_keep_the_handoff_values()
    {
        var presets = CatalogFiles.LoadObject(CatalogFiles.Catalog("touch-presets.json"))[
            "presets"
        ]!
            .AsArray()
            .ToDictionary(p => p!["id"]!.GetValue<string>(), p => p!, StringComparer.Ordinal);

        foreach (
            var (handoffId, id) in new[]
            {
                ("std", "standard"),
                ("leve", "mild-tremor"),
                ("fuerte", "strong-tremor"),
            }
        )
        {
            var handoff = Handoff.Value["PRESETS"]![handoffId]!;
            var preset = presets[id];
            TimingsCatalog
                .Duration(preset["debounce"]!.GetValue<string>())
                .TotalMilliseconds.ShouldBe(handoff["deb"]!.GetValue<int>());
            preset["hitSlopPx"]!.GetValue<int>().ShouldBe(handoff["hit"]!.GetValue<int>());
            preset["cancelMovePx"]!.GetValue<int>().ShouldBe(handoff["mov"]!.GetValue<int>());
            TimingsCatalog
                .Duration(preset["minContact"]!.GetValue<string>())
                .TotalMilliseconds.ShouldBe(handoff["min"]!.GetValue<int>());
        }
    }

    [Theory]
    [InlineData("S")]
    [InlineData("M")]
    [InlineData("L")]
    [Trait("Req", "CUA-007")]
    [Trait("Req", "FIJ-002")]
    public void Size_measures_keep_the_handoff_values(string sizeId)
    {
        var handoff = Handoff.Value["SIZES"]![sizeId]!;
        var size = CatalogFiles.LoadObject(CatalogFiles.Catalog("sizes.json"))["sizes"]!
            .AsArray()
            .Single(s => Ordinal.Is(s!["id"]!.GetValue<string>(), sizeId))!;

        var pairs = new (string Handoff, JsonNode? Catalog)[]
        {
            ("w", size["tile"]!["widthPx"]),
            ("h", size["tile"]!["heightPx"]),
            ("icon", size["tile"]!["iconPx"]),
            ("label", size["tile"]!["labelPx"]),
            ("foot", size["tile"]!["keysPx"]),
            ("gap", size["gapPx"]),
            ("hb", size["headerButton"]!["heightPx"]),
            ("sh", size["strip"]!["heightPx"]),
            ("sicon", size["strip"]!["iconPx"]),
            ("slabel", size["strip"]!["labelPx"]),
        };
        foreach (var (name, value) in pairs)
        {
            value!.GetValue<int>().ShouldBe(handoff[name]!.GetValue<int>(), name);
        }

        handoff.AsObject().Count.ShouldBe(pairs.Length, "every handoff measure is compared");
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void Mouse_actions_keep_the_handoff_labels_and_icons()
    {
        var actions = CatalogFiles.LoadObject(CatalogFiles.Catalog("mouse.json"))["actions"]!
            .AsArray()
            .ToDictionary(a => a!["id"]!.GetValue<string>(), a => a!, StringComparer.Ordinal);

        var handoff = Handoff.Value["MOUSE"]!.AsObject();
        actions.Keys.ShouldBe(handoff.Select(m => m.Key), ignoreOrder: true);
        foreach (var (id, value) in handoff)
        {
            actions[id]["label"]!["es"]!.GetValue<string>().ShouldBe(value![0]!.GetValue<string>());
            actions[id]["label"]!["en"]!.GetValue<string>().ShouldBe(value[1]!.GetValue<string>());
            actions[id]["icon"]!.GetValue<string>().ShouldBe(value[2]!.GetValue<string>());
        }
    }

    [Fact]
    [Trait("Req", "TEM-003")]
    public void Categories_keep_the_handoff_identifiers_and_order()
    {
        CatalogFiles.LoadObject(CatalogFiles.Catalog("categories.json"))["categories"]!
            .AsArray()
            .Select(c => c!["id"]!.GetValue<string>())
            .ShouldBe(Handoff.Value["CAT"]!.AsObject().Select(c => c.Key));
    }

    [Fact]
    [Trait("Req", "CAT-003")]
    public void Templates_are_the_six_of_the_handoff_plus_the_three_sample_profiles()
    {
        var expected = Handoff.Value["TPL"]!
            .AsArray()
            .Select(t => t!["id"]!.GetValue<string>())
            .Concat(ProfilesBecameTemplates.Values)
            .Order(StringComparer.Ordinal);

        ContentCatalog
            .Templates.Select(t => t["id"]!.GetValue<string>())
            .Order(StringComparer.Ordinal)
            .ShouldBe(expected);
    }

    [Fact]
    [Trait("Req", "CAT-003")]
    public void Template_identities_keep_the_handoff_names_icons_and_processes()
    {
        var sources = Handoff.Value["TPL"]!
            .AsArray()
            .Select(t => (Id: t!["id"]!.GetValue<string>(), Node: t))
            .Concat(
                ProfilesBecameTemplates.Select(p =>
                    (Id: p.Value, Node: Handoff.Value["SEED"]!["profiles"]![p.Key]!)
                )
            );

        foreach (var (id, source) in sources)
        {
            var template = ContentCatalog.Templates.Single(t =>
                Ordinal.Is(t["id"]!.GetValue<string>(), id)
            );
            template["name"]!["es"]!
                .GetValue<string>()
                .ShouldBe(source!["es"]!.GetValue<string>(), id);
            template["name"]!["en"]!
                .GetValue<string>()
                .ShouldBe(source["en"]!.GetValue<string>(), id);
            template["icon"]!.GetValue<string>().ShouldBe(source["icon"]!.GetValue<string>(), id);
            Strings(template["processes"]!)
                .ShouldBe([source["process"]!.GetValue<string>().ToLowerInvariant()], id);
            template["category"]
                ?.GetValue<string>()
                .ShouldBe(source["cat"]?.GetValue<string>(), id);
        }
    }

    [Fact]
    [Trait("Req", "CAT-003")]
    public void Initial_content_and_library_sections_keep_the_handoff_counts()
    {
        var seed = ContentCatalog.Seed;
        seed["alwaysVisible"]!.AsArray().Count.ShouldBe(4);
        seed["general"]!["shortcuts"]!.AsArray().Count.ShouldBe(12);
        seed["general"]!["icon"]!
            .GetValue<string>()
            .ShouldBe(Handoff.Value["SEED"]!["profiles"]!["general"]!["icon"]!.GetValue<string>());

        var sections = ContentCatalog.Library["sections"]!.AsArray();
        sections
            .Select(s => s!["id"]!.GetValue<string>())
            .ShouldBe(Handoff.Value["LIB"]!.AsObject().Select(s => s.Key));
        sections.Select(s => s!["shortcuts"]!.AsArray().Count).ShouldBe([7, 7, 5, 4, 3, 6]);
    }

    [Fact]
    [Trait("Req", "CAT-003")]
    [Trait("Req", "CAT-004")]
    public void Every_shortcut_keeps_its_name_icon_category_and_action_unless_a_decision_changed_it()
    {
        var failures = new List<string>();
        var seen = new HashSet<(string, string)>();
        foreach (var (container, source, catalog) in Containers())
        {
            var byId = catalog.ToDictionary(s => s.Id, StringComparer.Ordinal);
            foreach (var button in source)
            {
                var handoffId = button["id"]!.GetValue<string>();
                var id = Ordinal.Is(container, "library") ? handoffId["l_".Length..] : handoffId;
                var key = (container, handoffId);
                seen.Add(key);
                if (RemovedShortcuts.Contains(key))
                {
                    if (byId.ContainsKey(id))
                    {
                        failures.Add(
                            $"{container}/{handoffId} was removed on purpose but is present."
                        );
                    }

                    continue;
                }

                if (!byId.TryGetValue(id, out var shortcut))
                {
                    failures.Add($"{container}/{handoffId} is missing.");
                    continue;
                }

                Compare(failures, container, handoffId, button, shortcut);
            }

            if (
                catalog.Count
                != source.Count - RemovedShortcuts.Count(r => Ordinal.Is(r.Container, container))
            )
            {
                failures.Add(
                    $"{container} has {catalog.Count} shortcuts; the handoff has {source.Count}."
                );
            }
        }

        ChangedActions
            .Keys.Concat(RemovedShortcuts)
            .Where(k => !seen.Contains(k))
            .Select(k => $"Stale decision {k}")
            .ShouldBeEmpty();
        failures.ShouldBeEmpty();
    }

    private static void Compare(
        List<string> failures,
        string container,
        string handoffId,
        JsonObject button,
        ContentShortcut shortcut
    )
    {
        var where = container + "/" + handoffId;
        if (
            !Ordinal.Is(shortcut.NameEs, button["es"]!.GetValue<string>())
            || !Ordinal.Is(shortcut.NameEn, button["en"]!.GetValue<string>())
        )
        {
            failures.Add(where + ": name differs.");
        }

        if (!Ordinal.Is(shortcut.Icon, button["icon"]!.GetValue<string>()))
        {
            failures.Add(where + ": icon differs.");
        }

        if (!Ordinal.Is(shortcut.Category, button["cat"]!.GetValue<string>()))
        {
            failures.Add(where + ": category differs.");
        }

        if (shortcut.Confirm != (button["confirm"]?.GetValue<bool>() ?? false))
        {
            failures.Add(where + ": confirmation differs.");
        }

        var converted = Convert(button);
        if (ChangedActions.TryGetValue((container, handoffId), out var changed))
        {
            if (JsonNode.DeepEquals(converted, shortcut.Action))
            {
                failures.Add(
                    where + ": listed as changed but equals the handoff; remove the decision."
                );
            }

            converted = JsonNode.Parse(changed)!.AsObject();
        }

        if (!JsonNode.DeepEquals(converted, shortcut.Action))
        {
            failures.Add(
                $"{where}: action {shortcut.Action.ToJsonString()} differs from {converted.ToJsonString()}."
            );
        }
    }

    /// <summary>The handoff button translated literally to the catalog action format.</summary>
    private static JsonObject Convert(JsonObject button)
    {
        var type = button["type"]!.GetValue<string>();
        switch (type)
        {
            case "tap" or "hold" or "toggle":
                var action = new JsonObject
                {
                    ["type"] = type,
                    ["keys"] = KeyArray(button["keys"]!),
                };
                if (button["vk"] is JsonObject variants)
                {
                    action["variants"] = new JsonObject(
                        variants.Select(v =>
                            KeyValuePair.Create(v.Key, (JsonNode?)KeyArray(v.Value!))
                        )
                    );
                }

                return action;
            case "mouse":
                return new JsonObject
                {
                    ["type"] = "mouse",
                    ["mouse"] = button["mouse"]!.GetValue<string>(),
                };
            case "url":
                return new JsonObject
                {
                    ["type"] = "web",
                    ["url"] = button["url"]!.GetValue<string>(),
                };
            case "text":
                return new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = button["text"]!.GetValue<string>(),
                };
            case "macro":
                var steps = new JsonArray();
                foreach (var step in button["steps"]!.AsArray())
                {
                    var kind = step!["kind"]!.GetValue<string>();
                    steps.Add(
                        kind switch
                        {
                            "keys" => new JsonObject
                            {
                                ["kind"] = "keys",
                                ["keys"] = KeyArray(step["keys"]!),
                            },
                            "wait" => new JsonObject
                            {
                                ["kind"] = "wait",
                                ["ms"] = step["ms"]!.GetValue<int>(),
                            },
                            "text" => new JsonObject
                            {
                                ["kind"] = "text",
                                ["text"] = step["text"]!.GetValue<string>(),
                            },
                            _ => new JsonObject
                            {
                                ["kind"] = "mouse",
                                ["mouse"] = step["mouse"]!.GetValue<string>(),
                            },
                        }
                    );
                }

                return new JsonObject { ["type"] = "macro", ["steps"] = steps };
            default:
                throw new InvalidDataException("Unknown handoff type " + type);
        }
    }

    private static JsonArray KeyArray(JsonNode labels) =>
        new([
            .. Strings(labels)
                .Select(label =>
                    (JsonNode?)
                        JsonValue.Create(
                            KeyCatalog.Shared.Resolve(label)?.Id ?? "<unknown " + label + ">"
                        )
                ),
        ]);

    private static IEnumerable<(
        string Container,
        IReadOnlyList<JsonObject> Source,
        IReadOnlyList<ContentShortcut> Catalog
    )> Containers()
    {
        var seed = Handoff.Value["SEED"]!;
        yield return ("always", Objects(seed["global"]!), List("seed:alwaysVisible"));
        yield return (
            "general",
            Objects(seed["profiles"]!["general"]!["buttons"]!),
            List("seed:general")
        );
        foreach (var (profile, template) in ProfilesBecameTemplates)
        {
            yield return (
                profile,
                Objects(seed["profiles"]![profile]!["buttons"]!),
                List("template:" + template)
            );
        }

        foreach (var template in Handoff.Value["TPL"]!.AsArray())
        {
            var id = template!["id"]!.GetValue<string>();
            yield return (id, Objects(template["buttons"]!), List("template:" + id));
        }

        var library = Handoff.Value["LIB"]!
            .AsObject()
            .SelectMany(section => Objects(section.Value!))
            .ToList();
        yield return (
            "library",
            library,
            [
                .. ContentCatalog
                    .Lists.Where(l => l.Name.StartsWith("library:", StringComparison.Ordinal))
                    .SelectMany(l => l.Shortcuts),
            ]
        );
    }

    private static IReadOnlyList<ContentShortcut> List(string name) =>
        ContentCatalog.Lists.Single(l => Ordinal.Is(l.Name, name)).Shortcuts;

    private static IReadOnlyList<JsonObject> Objects(JsonNode array) =>
        [.. array.AsArray().Select(n => n!.AsObject())];

    private static IEnumerable<string> HandoffIcons()
    {
        var seed = Handoff.Value;
        IEnumerable<JsonNode> Buttons() =>
            seed["SEED"]!["global"]!
                .AsArray()!
                .Concat(
                    seed["SEED"]!["profiles"]!
                        .AsObject()
                        .SelectMany(p => p.Value!["buttons"]!.AsArray())
                )
                .Concat(seed["TPL"]!.AsArray().SelectMany(t => t!["buttons"]!.AsArray()))
                .Concat(seed["LIB"]!.AsObject().SelectMany(s => s.Value!.AsArray()))
                .Concat(seed["SCROLL"]!.AsArray())!;

        return Buttons()
            .Select(b => b["icon"]!.GetValue<string>())
            .Concat(
                seed["SEED"]!["profiles"]!
                    .AsObject()
                    .Select(p => p.Value!["icon"]!.GetValue<string>())
            )
            .Concat(seed["TPL"]!.AsArray().Select(t => t!["icon"]!.GetValue<string>()))
            .Concat(seed["MOUSE"]!.AsObject().Select(m => m.Value![2]!.GetValue<string>()))
            .Concat(Strings(seed["ICONS"]!))
            .Concat(seed["KEYICON"]!.AsObject().Select(k => k.Value!.GetValue<string>()))
            .Concat(seed["APPS"]!.AsArray().Select(a => a!["icon"]!.GetValue<string>()))
            .Distinct(StringComparer.Ordinal);
    }

    private static IReadOnlyList<(string Id, IReadOnlyList<string> Words)> Icons() =>
        [
            .. CatalogFiles.LoadObject(CatalogFiles.Catalog("icons.json"))["icons"]!
                .AsArray()
                .Select(i =>
                    (
                        i!["id"]!.GetValue<string>(),
                        (IReadOnlyList<string>)
                            [.. Strings(i["tags"]!["es"]!), .. Strings(i["tags"]!["en"]!)]
                    )
                ),
        ];

    /// <summary>Lower case without diacritics: the handoff tags were written without accents.</summary>
    private static string Fold(string word)
    {
        var decomposed = word.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string([
            .. decomposed.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
            ),
        ]);
    }

    private static IReadOnlyList<string> Strings(JsonNode node) =>
        ContentShortcut.Strings(node.AsArray());
}
