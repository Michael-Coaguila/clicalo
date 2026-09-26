using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The welcome state.</summary>
internal sealed record OnboardingDto
{
    public bool? Completed { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
