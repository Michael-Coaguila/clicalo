using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Timing;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// The structural check of an AI answer (blueprint §9.2, step 1): the same rules as
/// <c>data/schemas/ai-template.v1.schema.json</c>, in code, on strict JSON (no comments, trailing commas or repeated
/// properties) of at most <c>Timings.Ai.AiResponseMaxBytes</c>. Anything else is «invalid» (PLA-008). The answer may
/// wrap the object in a code fence; the text around the outermost braces is ignored.
/// </summary>
internal static partial class AiResponseReader
{
    private const int MaxKeys = 8;
    private const int MaxDepth = 8;
    private const int ProcessMaxLength = 64;

    private static readonly FrozenSet<string> RootProperties = new[]
    {
        "known",
        "app",
        "process",
        "icon",
        "buttons",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ButtonProperties = new[]
    {
        "name",
        "icon",
        "keys",
        "cat",
        "confidence",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> NameProperties = new[] { "es", "en" }.ToFrozenSet(
        StringComparer.Ordinal
    );

    /// <summary>The proposal of <paramref name="answer"/>, or <see langword="null"/> when it breaks the contract.</summary>
    /// <param name="answer">The text of the answer.</param>
    public static AiTemplateProposal? Read(string? answer)
    {
        if (string.IsNullOrEmpty(answer))
        {
            return null;
        }

        var start = answer.IndexOf('{', StringComparison.Ordinal);
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        var json = answer[start..(end + 1)];
        if (Encoding.UTF8.GetByteCount(json) > Timings.Ai.AiResponseMaxBytes)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    AllowDuplicateProperties = false,
                    MaxDepth = MaxDepth,
                }
            );
            return Proposal(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AiTemplateProposal? Proposal(JsonElement root)
    {
        if (
            !OnlyProperties(root, RootProperties)
            || !root.TryGetProperty("known", out var known)
            || known.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || Text(root, "app", 1, Timings.Ai.AiAppNameMaxLength) is not { } app
            || Icon(root) is not { } icon
            || !root.TryGetProperty("buttons", out var buttons)
            || buttons.ValueKind != JsonValueKind.Array
            || buttons.GetArrayLength() is < 1 or > Timings.Ai.AiMaxShortcuts
        )
        {
            return null;
        }

        var process = string.Empty;
        if (root.TryGetProperty("process", out var processNode))
        {
            if (
                processNode.ValueKind != JsonValueKind.String
                || processNode.GetString()!.Length > ProcessMaxLength
            )
            {
                return null;
            }

            process = processNode.GetString()!;
        }

        var shortcuts = new List<AiProposedShortcut>(buttons.GetArrayLength());
        foreach (var button in buttons.EnumerateArray())
        {
            if (Shortcut(button) is not { } shortcut)
            {
                return null;
            }

            shortcuts.Add(shortcut);
        }

        return new AiTemplateProposal(known.GetBoolean(), app, process, icon, [.. shortcuts]);
    }

    private static AiProposedShortcut? Shortcut(JsonElement button)
    {
        if (
            !OnlyProperties(button, ButtonProperties)
            || !button.TryGetProperty("name", out var name)
            || !OnlyProperties(name, NameProperties)
            || Text(name, "es", 1, Timings.Ai.AiNameMaxLength) is not { } es
            || Text(name, "en", 1, Timings.Ai.AiNameMaxLength) is not { } en
            || Icon(button) is not { } icon
            || !button.TryGetProperty("keys", out var keys)
            || keys.ValueKind != JsonValueKind.Array
            || keys.GetArrayLength() is < 1 or > MaxKeys
            || Text(button, "cat", 1, 8) is not { } category
            || !TemplateSchema.Categories.Contains(category)
        )
        {
            return null;
        }

        var ids = new List<string>(keys.GetArrayLength());
        foreach (var key in keys.EnumerateArray())
        {
            if (key.ValueKind != JsonValueKind.String || !KeyId().IsMatch(key.GetString()!))
            {
                return null;
            }

            ids.Add(key.GetString()!);
        }

        var confidence = 1d;
        if (button.TryGetProperty("confidence", out var value))
        {
            if (
                value.ValueKind != JsonValueKind.Number
                || !value.TryGetDouble(out confidence)
                || confidence is < 0 or > 1
            )
            {
                return null;
            }
        }

        return new AiProposedShortcut(es, en, icon, new ValueList<string>([.. ids]), category, confidence);
    }

    private static bool OnlyProperties(JsonElement element, FrozenSet<string> allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static string? Text(JsonElement element, string property, int min, int max) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text
        && text.Length >= min
        && text.Length <= max
            ? text
            : null;

    private static string? Icon(JsonElement element) =>
        Text(element, "icon", 1, 40) is { } icon && IconId().IsMatch(icon) ? icon : null;

    [GeneratedRegex(
        "^[a-z][a-z0-9_]{0,39}$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex IconId();

    [GeneratedRegex(
        @"^(?:[a-z0-9]+(?:\.[a-z0-9]+)*|char:[^\sA-Za-z0-9])$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex KeyId();
}
