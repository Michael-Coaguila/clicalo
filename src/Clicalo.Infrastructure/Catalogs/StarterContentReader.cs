using System.Text.Json;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// Reads the three kinds of starter content (D17: data loaded at run time) and validates them again, since content is
/// untrusted (LOG-006): <c>starter.json</c> (the kit, user decision D2), <c>seed.json</c> («Basics», CAT-003) and
/// <c>templates/&lt;id&gt;.json</c> (CAT-006). A file that does not validate gives <see langword="null"/>; a shortcut
/// that does not validate is left out (<see cref="ContentShortcutReader"/>).
/// </summary>
public static class StarterContentReader
{
    /// <summary>The kit of <paramref name="json"/>, or <see langword="null"/>.</summary>
    /// <param name="json">The bytes of <c>starter.json</c>.</param>
    /// <remarks>
    /// Valid when its option ids are unique, exactly one option is «Basics» and the texts of «Basics» exist in
    /// <c>data/i18n</c>.
    /// </remarks>
    public static StarterKit? ReadKit(ReadOnlyMemory<byte> json) => ContentJson.Read(json, KitOf);

    /// <summary>The «Basics» content of <paramref name="json"/>, or <see langword="null"/>.</summary>
    /// <param name="json">The bytes of <c>seed.json</c>.</param>
    public static SeedContent? ReadSeed(ReadOnlyMemory<byte> json) =>
        ContentJson.Read(
            json,
            static root =>
            {
                var general = root.GetProperty("general");
                return new SeedContent(
                    Positive(root.GetProperty("catalogVersion").GetInt32()),
                    ContentShortcutReader.ReadAll(root.GetProperty("alwaysVisible")),
                    ContentJson.Text(general.GetProperty("name")),
                    new IconRef(ContentJson.String(general, "icon")),
                    ContentShortcutReader.ReadAll(general.GetProperty("shortcuts"))
                );
            }
        );

    /// <summary>
    /// The template of <paramref name="json"/>, or <see langword="null"/> when it does not validate or its id is not
    /// <paramref name="expectedId"/> (the id is the file name, CAT-006).
    /// </summary>
    /// <param name="json">The bytes of the template file.</param>
    /// <param name="expectedId">The file name without extension.</param>
    public static ProfileTemplate? ReadTemplate(ReadOnlyMemory<byte> json, string expectedId) =>
        ContentJson.Read(
            json,
            root =>
            {
                var id = ContentJson.String(root, "id");
                var processes = Processes(root.GetProperty("processes"));
                var shortcuts = ContentShortcutReader.ReadAll(root.GetProperty("shortcuts"));
                return
                    !string.Equals(id, expectedId, StringComparison.Ordinal)
                    || processes.IsEmpty
                    || shortcuts.IsEmpty
                    ? null
                    : new ProfileTemplate(
                        id,
                        Positive(root.GetProperty("version").GetInt32()),
                        ContentJson.Text(root.GetProperty("name")),
                        new IconRef(ContentJson.String(root, "icon")),
                        processes,
                        shortcuts
                    );
            }
        );

    private static StarterKit? KitOf(JsonElement root)
    {
        var options = new List<StarterOption>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty("options").EnumerateArray())
        {
            var id = ContentJson.String(item, "id");
            var selected = item.GetProperty("selected").GetBoolean();
            StarterOption option = ContentJson.String(item, "kind") switch
            {
                "basics" => new StarterOption(
                    id,
                    StarterOptionKind.Basics,
                    selected,
                    new IconRef(ContentJson.String(item, "icon")),
                    Key(ContentJson.String(item, "labelKey")),
                    Key(ContentJson.String(item, "descriptionKey"))
                ),
                "template" => new StarterOption(id, StarterOptionKind.Template, selected),
                _ => throw new FormatException("Unknown option kind."),
            };
            if (!ids.Add(id))
            {
                return null;
            }

            options.Add(option);
        }

        return options.Count(static o => o.Kind == StarterOptionKind.Basics) == 1
            ? new StarterKit(Positive(root.GetProperty("version").GetInt32()), [.. options])
            : null;
    }

    private static MessageKey Key(string key) =>
        MessageCatalog.TryGet(key, out var descriptor)
            ? descriptor.Key
            : throw new FormatException("Unknown text key.");

    private static ValueList<ProcessName> Processes(JsonElement array)
    {
        var processes = new List<ProcessName>();
        foreach (var item in array.EnumerateArray())
        {
            var process = new ProcessName(item.GetString() ?? string.Empty);
            if (
                process.IsEmpty
                || process.Value.AsSpan().IndexOfAny(['\\', '/', ':']) >= 0
                || processes.Contains(process)
            )
            {
                throw new FormatException("A process is empty, has a path or is repeated.");
            }

            processes.Add(process);
        }

        return [.. processes];
    }

    private static int Positive(int version) =>
        version >= 1 ? version : throw new FormatException("A version is not positive.");
}
