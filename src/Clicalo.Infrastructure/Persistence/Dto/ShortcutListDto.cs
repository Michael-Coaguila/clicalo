using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The Always visible row.</summary>
internal sealed record ShortcutListDto
{
    public List<ShortcutDto>? Shortcuts { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
