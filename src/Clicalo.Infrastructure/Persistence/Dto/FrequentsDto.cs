using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>Frequents curation; <c>usageEpoch</c> must match the one of <c>usage.json</c> (§6.5).</summary>
internal sealed record FrequentsDto
{
    public List<string>? Pins { get; init; }

    public List<string>? Hidden { get; init; }

    public long? UsageEpoch { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
