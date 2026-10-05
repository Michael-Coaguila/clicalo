using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>
/// The <c>payload</c> of <c>usage.json</c> (§6.5): the epoch it belongs to and the executions per shortcut in Unix
/// milliseconds, keys in ordinal order.
/// </summary>
internal sealed record UsagePayloadDto
{
    public long? UsageEpoch { get; init; }

    public Dictionary<string, List<long>>? Usage { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
