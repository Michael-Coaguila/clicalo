using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>
/// An action, discriminated by <c>type</c> (the closed hierarchy of blueprint §6.2): <c>tap</c>, <c>hold</c>,
/// <c>toggle</c>, <c>text</c>, <c>mouse</c>, <c>macro</c>, <c>url</c>, <c>app</c> or <c>system</c>. Keys are written in
/// press order (EJE-003). A text is <c>{"enc":"dpapi.v1","blob":"…","len":42,"private":true}</c> (LOG-003).
/// </summary>
internal sealed record ActionDto
{
    public string? Type { get; init; }

    public List<string>? Keys { get; init; }

    public List<VariantDto>? Variants { get; init; }

    public JsonNode? Text { get; init; }

    public string? Method { get; init; }

    public string? Mouse { get; init; }

    public string? Speed { get; init; }

    public List<StepDto>? Steps { get; init; }

    public string? Url { get; init; }

    public bool? Raw { get; init; }

    public AppTargetDto? App { get; init; }

    public string? Command { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting (§6.5).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
