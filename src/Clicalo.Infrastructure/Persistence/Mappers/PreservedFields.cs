using System.Collections.Immutable;
using System.Text.Json;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// What the Domain does not model but a rewrite must keep (blueprint §6.5, REG-08): the members of a later minor at the
/// root, in the settings, the Always visible row, Frequents, the welcome, each profile, each shortcut, its action and
/// its steps; and the original blob of every text that cannot be decrypted here, so saving never destroys it.
/// Captured when the live document is read; immutable.
/// </summary>
internal sealed record PreservedFields
{
    /// <summary>Nothing to keep.</summary>
    public static PreservedFields None { get; } = new();

    public IReadOnlyDictionary<string, JsonElement>? Root { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? Settings { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? Always { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? Frequents { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? Onboarding { get; init; }

    /// <summary>By profile id.</summary>
    public ImmutableDictionary<
        string,
        IReadOnlyDictionary<string, JsonElement>
    > Profiles { get; init; } =
        ImmutableDictionary.Create<string, IReadOnlyDictionary<string, JsonElement>>(
            StringComparer.Ordinal
        );

    /// <summary>By shortcut id.</summary>
    public ImmutableDictionary<string, ShortcutExtras> Shortcuts { get; init; } =
        ImmutableDictionary.Create<string, ShortcutExtras>(StringComparer.Ordinal);

    /// <summary>The encrypted texts that were unavailable when read, by where they were.</summary>
    public ImmutableDictionary<TextSlot, JsonElement> Texts { get; init; } =
        ImmutableDictionary<TextSlot, JsonElement>.Empty;

    /// <summary>The unknown members of one shortcut, its action (when its type is unchanged) and its steps (by index and kind).</summary>
    /// <param name="Shortcut">Members of the shortcut.</param>
    /// <param name="ActionType">The persisted action type they were read with.</param>
    /// <param name="Action">Members of the action.</param>
    /// <param name="Steps">Members of each step, with the step kind they were read with.</param>
    internal sealed record ShortcutExtras(
        IReadOnlyDictionary<string, JsonElement>? Shortcut,
        string? ActionType,
        IReadOnlyDictionary<string, JsonElement>? Action,
        ImmutableArray<(string? Kind, IReadOnlyDictionary<string, JsonElement>? Extra)> Steps
    );

    /// <summary>Where a text lives: the text of a Text action (<paramref name="Step"/> −1) or of a macro step.</summary>
    /// <param name="ShortcutId">The shortcut.</param>
    /// <param name="Step">The step index, or −1.</param>
    internal readonly record struct TextSlot(string ShortcutId, int Step);
}
