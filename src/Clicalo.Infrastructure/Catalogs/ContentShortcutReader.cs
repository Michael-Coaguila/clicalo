using System.Text.Json;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// A shortcut of the starter content (<c>data/schemas/shortcut.schema.json</c>), validated again when loaded because
/// content is untrusted (LOG-006). The kit uses tap (with its language variants, CAT-005), hold, toggle, mouse, macro
/// and web; a shortcut of another kind (text, app or system, which only the library offers) or one that does not
/// validate (a key outside the catalog, a wait out of range, an address that is not http or https) is left out rather
/// than guessed.
/// </summary>
internal static class ContentShortcutReader
{
    /// <summary>The shortcuts of <paramref name="array"/> that validate, in order.</summary>
    /// <param name="array">The JSON array of shortcuts.</param>
    public static ValueList<TemplateShortcut> ReadAll(JsonElement array)
    {
        var shortcuts = new List<TemplateShortcut>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            if (Read(item) is { } shortcut && ids.Add(shortcut.ItemId))
            {
                shortcuts.Add(shortcut);
            }
        }

        return [.. shortcuts];
    }

    private static TemplateShortcut? Read(JsonElement item)
    {
        if (Action(item.GetProperty("action")) is not { } action)
        {
            return null;
        }

        return new TemplateShortcut(
            ContentJson.String(item, "id"),
            ContentJson.Text(item.GetProperty("name")),
            new IconRef(ContentJson.String(item, "icon")),
            new CategoryId(ContentJson.String(item, "category")),
            action,
            item.TryGetProperty("confirm", out var confirm) && confirm.GetBoolean()
        );
    }

    private static ShortcutAction? Action(JsonElement action) =>
        action.GetProperty("type").GetString() switch
        {
            "tap" => Tap(action),
            "hold" => ContentJson.Chord(action.GetProperty("keys")) is { } chord
                ? new HoldAction(chord)
                : null,
            "toggle" => ContentJson.Chord(action.GetProperty("keys")) is { } chord
                ? new ToggleAction(chord)
                : null,
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
            "macro" => Macro(action.GetProperty("steps")),
            "web" => Web(action.GetProperty("url").GetString()),
            _ => null,
        };

    private static TapAction? Tap(JsonElement action)
    {
        if (ContentJson.Chord(action.GetProperty("keys")) is not { } chord)
        {
            return null;
        }

        var variants = new List<ChordVariant>();
        if (action.TryGetProperty("variants", out var byLanguage))
        {
            foreach (var variant in byLanguage.EnumerateObject())
            {
                if (ContentJson.Chord(variant.Value) is not { } other)
                {
                    return null;
                }

                variants.Add(new ChordVariant(new LangCode(variant.Name), other));
            }
        }

        return new TapAction(chord, [.. variants]);
    }

    private static MacroAction? Macro(JsonElement steps)
    {
        var read = new List<MacroStep>();
        foreach (var step in steps.EnumerateArray())
        {
            MacroStep? next = step.GetProperty("kind").GetString() switch
            {
                "keys" => ContentJson.Chord(step.GetProperty("keys")) is { } chord
                    ? new KeysStep(chord)
                    : null,
                "wait" => Wait(step.GetProperty("ms").GetInt32()),
                "text" => step.GetProperty("text").GetString() is { Length: > 0 } text
                    ? new TextStep(SecretText.From(text))
                    : null,
                "mouse"
                    when PersistedNames.Mouse.TryParse(
                        step.GetProperty("mouse").GetString(),
                        out var op
                    ) => new MouseStep(op),
                _ => null,
            };
            if (next is null)
            {
                return null;
            }

            read.Add(next);
        }

        return read.Count == 0 ? null : new MacroAction([.. read]);
    }

    private static WaitStep? Wait(int milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        return
            duration < Timings.Macro.MacroWaitRange.Min
            || duration > Timings.Macro.MacroWaitRange.Max
            ? null
            : new WaitStep(duration);
    }

    private static UrlAction? Web(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
        )
            ? new UrlAction(new UrlTarget.Valid(uri))
            : null;
}
