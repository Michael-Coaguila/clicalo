using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>A macro step: <c>keys</c>, <c>wait</c> (<c>ms</c>), <c>text</c> or <c>mouse</c>.</summary>
internal sealed record StepDto
{
    public string? Kind { get; init; }

    public List<string>? Keys { get; init; }

    public double? Ms { get; init; }

    public JsonNode? Text { get; init; }

    public string? Mouse { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting (§6.5).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
