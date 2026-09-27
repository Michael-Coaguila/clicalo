using System.Globalization;
using System.Text.Json;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Content;

/// <summary>
/// The document of a new installation (CAT-003, PQ-44): the Always visible row and the General profile of
/// <c>data/content/seed.json</c>, loaded at run time (D17) and validated again here, since content is untrusted
/// (LOG-006). Only the actions a first document needs are read (tap, hold, toggle and mouse); a shortcut of any other
/// kind, or one that does not validate, is left out rather than guessed.
/// </summary>
public static class SeedDocument
{
    /// <summary>The source of the catalog reference (DAT-004) of every seed shortcut.</summary>
    public const string Source = "seed";

    /// <summary>The name of the file in the content folder.</summary>
    public const string FileName = "seed.json";

    /// <summary>
    /// The new document of <paramref name="json"/>, with <paramref name="settings"/>; <see langword="null"/> when the
    /// file is not a readable seed or its library does not validate (the caller keeps a minimal document).
    /// </summary>
    /// <param name="json">The bytes of <c>seed.json</c>.</param>
    /// <param name="settings">The settings of the new document.</param>
    public static UserDocument? Read(ReadOnlyMemory<byte> json, UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.GetProperty("catalogVersion")
                .GetInt32()
                .ToString(CultureInfo.InvariantCulture);
            var always = Shortcuts(root.GetProperty("alwaysVisible"), version);
            var general = root.GetProperty("general");
            var profile = new Profile(
                ProfileId.General,
                Text(general.GetProperty("name")),
                new IconRef(general.GetProperty("icon").GetString() ?? "apps"),
                false,
                new AppBinding.Manual(),
                InjectionMode.VirtualKey,
                Shortcuts(general.GetProperty("shortcuts"), version),
                null
            );
            return ShortcutLibrary.CreateValidated(always, [profile]).TryGetValue(out var library)
                ? UserDocument.Create(library, settings)
                : null;
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
            )
        {
            return null;
        }
    }

    private static ValueList<Shortcut> Shortcuts(JsonElement array, string version)
    {
        var shortcuts = new List<Shortcut>();
        foreach (var item in array.EnumerateArray())
        {
            if (Action(item.GetProperty("action")) is not { } action)
            {
                continue;
            }

            var id = item.GetProperty("id").GetString() ?? string.Empty;
            shortcuts.Add(
                new Shortcut(
                    new ShortcutId(id),
                    Text(item.GetProperty("name")),
                    new IconRef(item.GetProperty("icon").GetString() ?? "bolt"),
                    false,
                    new CategoryId(item.GetProperty("category").GetString() ?? string.Empty),
                    action,
                    new ShortcutOptions(
                        item.TryGetProperty("confirm", out var confirm) && confirm.GetBoolean(),
                        new HoldLimit.InheritGlobal(),
                        false
                    ),
                    new CatalogRef(Source, version, id),
                    null
                )
            );
        }

        return [.. shortcuts];
    }

    private static ShortcutAction? Action(JsonElement action) =>
        action.GetProperty("type").GetString() switch
        {
            "tap" => new TapAction(Chord(action), []),
            "hold" => new HoldAction(Chord(action)),
            "toggle" => new ToggleAction(Chord(action)),
            "mouse"
                when PersistedNames.Mouse.TryParse(
                    action.GetProperty("mouse").GetString(),
                    out var op
                ) => new MouseAction(
                op,
                PersistedNames.Speed.ParseOr(
                    action.TryGetProperty("speed", out var speed) ? speed.GetString() : null,
                    ScrollSpeed.Normal
                )
            ),
            _ => null,
        };

    private static KeyChord Chord(JsonElement action) =>
        KeyChord.Create([
            .. action
                .GetProperty("keys")
                .EnumerateArray()
                .Select(static key => new KeyStroke(new KeyId(key.GetString() ?? string.Empty))),
        ]);

    private static LocalizedText Text(JsonElement text) =>
        new(
            text.EnumerateObject()
                .Where(static entry => entry.Value.ValueKind == JsonValueKind.String)
                .Select(static entry =>
                    KeyValuePair.Create(new LangCode(entry.Name), entry.Value.GetString()!)
                )
        );
}
