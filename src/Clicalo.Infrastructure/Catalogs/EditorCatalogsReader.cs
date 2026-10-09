using System.Text.Json;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// Reads the data of the shortcut editor (D17), validated again because it is untrusted (LOG-006): the icon library
/// (<c>icons.json</c>, EDI-004), the icons of combinations (<c>combo-icons.&lt;lang&gt;.json</c>, EDI-005), the mouse
/// actions (<c>mouse.json</c>, EDI-012) and the «Añadir atajo» library (<c>library.json</c>, ATJ-010). A file that does
/// not read gives null; an entry that does not validate is left out.
/// </summary>
public static class EditorCatalogsReader
{
    /// <summary>The icon library, or null.</summary>
    /// <param name="json">The bytes of <c>icons.json</c>.</param>
    public static IconCatalog? ReadIcons(ReadOnlyMemory<byte> json) =>
        ContentJson.Read(
            json,
            static root =>
            {
                var entries = new List<IconEntry>();
                foreach (var icon in root.GetProperty("icons").EnumerateArray())
                {
                    var tags = new List<string>();
                    foreach (var language in icon.GetProperty("tags").EnumerateObject())
                    {
                        tags.AddRange(
                            language
                                .Value.EnumerateArray()
                                .Select(static t => t.GetString())
                                .OfType<string>()
                                .Where(static t => t.Length > 0)
                        );
                    }

                    entries.Add(
                        new IconEntry(new IconRef(ContentJson.String(icon, "id")), [.. tags])
                    );
                }

                return new IconCatalog(
                    entries,
                    Names(root.GetProperty("featured")),
                    Names(root.GetProperty("profileFeatured"))
                );
            }
        );

    /// <summary>The icons of combinations of one programs language, or null.</summary>
    /// <param name="json">The bytes of <c>combo-icons.&lt;lang&gt;.json</c>.</param>
    public static IReadOnlyList<(LangCode Language, KeyChord Chord, IconRef Icon)>? ReadComboIcons(
        ReadOnlyMemory<byte> json
    ) =>
        ContentJson.Read<IReadOnlyList<(LangCode, KeyChord, IconRef)>>(
            json,
            static root =>
            {
                var language = new LangCode(ContentJson.String(root, "appsLanguage"));
                var entries = new List<(LangCode, KeyChord, IconRef)>();
                foreach (var entry in root.GetProperty("entries").EnumerateArray())
                {
                    if (ContentJson.Chord(entry.GetProperty("keys")) is { } chord)
                    {
                        entries.Add(
                            (language, chord, new IconRef(ContentJson.String(entry, "icon")))
                        );
                    }
                }

                return entries;
            }
        );

    /// <summary>The eight mouse actions in order, or null.</summary>
    /// <param name="json">The bytes of <c>mouse.json</c>.</param>
    public static IReadOnlyList<MouseActionInfo>? ReadMouse(ReadOnlyMemory<byte> json) =>
        ContentJson.Read<IReadOnlyList<MouseActionInfo>>(
            json,
            static root =>
            {
                var actions = new List<MouseActionInfo>();
                foreach (var action in root.GetProperty("actions").EnumerateArray())
                {
                    if (
                        !PersistedNames.Mouse.TryParse(ContentJson.String(action, "id"), out var op)
                    )
                    {
                        continue;
                    }

                    var label = ContentJson.Text(action.GetProperty("label"));
                    var spoken = action.TryGetProperty("spoken", out var words)
                        ? ContentJson.Text(words)
                        : label;
                    actions.Add(
                        new MouseActionInfo(
                            op,
                            new IconRef(ContentJson.String(action, "icon")),
                            label,
                            spoken
                        )
                    );
                }

                return actions;
            }
        );

    /// <summary>The «Añadir atajo» library, or null.</summary>
    /// <param name="json">The bytes of <c>library.json</c>.</param>
    public static LibraryContent? ReadLibrary(ReadOnlyMemory<byte> json) =>
        ContentJson.Read(
            json,
            static root =>
            {
                var sections = new List<LibrarySection>();
                foreach (var section in root.GetProperty("sections").EnumerateArray())
                {
                    sections.Add(
                        new LibrarySection(
                            ContentJson.String(section, "id"),
                            ContentShortcutReader.ReadAll(
                                section.GetProperty("shortcuts"),
                                library: true
                            )
                        )
                    );
                }

                return new LibraryContent(
                    root.GetProperty("catalogVersion").GetInt32(),
                    [.. sections]
                );
            }
        );

    private static List<IconRef> Names(JsonElement array) =>
        array
            .EnumerateArray()
            .Select(static n => n.GetString())
            .OfType<string>()
            .Where(static n => n.Length > 0)
            .Select(static n => new IconRef(n))
            .ToList();
}
