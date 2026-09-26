using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>
/// A shortcut. <c>maxHold</c> is <c>inherit</c> (default), <c>never</c> or <c>after</c> with <c>maxHoldMs</c>;
/// <c>private</c> hides a text from the tile, notices and search (LOG-004).
/// </summary>
internal sealed record ShortcutDto
{
    public string? Id { get; init; }

    public Dictionary<string, string>? Name { get; init; }

    public string? Icon { get; init; }

    public bool? AutoIcon { get; init; }

    public string? Category { get; init; }

    public ActionDto? Action { get; init; }

    public bool? Confirm { get; init; }

    public string? MaxHold { get; init; }

    public double? MaxHoldMs { get; init; }

    public bool? Private { get; init; }

    public CatalogRefDto? Origin { get; init; }

    public string? PinnedFrom { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting (§6.5).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
