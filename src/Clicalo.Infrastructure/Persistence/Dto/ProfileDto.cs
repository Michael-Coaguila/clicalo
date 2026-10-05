using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>A profile. <c>processes</c> absent is a manual profile; <c>compat</c> is the scan-code injection mode.</summary>
internal sealed record ProfileDto
{
    public string? Id { get; init; }

    public Dictionary<string, string>? Name { get; init; }

    public string? Icon { get; init; }

    public bool? AutoIcon { get; init; }

    public List<string>? Processes { get; init; }

    public bool? Compat { get; init; }

    public List<ShortcutDto>? Shortcuts { get; init; }

    public CatalogRefDto? Origin { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting (§6.5).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
