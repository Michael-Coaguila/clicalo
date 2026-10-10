using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The welcome state; <c>uses</c>, <c>kit</c> and <c>baseline</c> since 1.1 (BIE-010, ADR-0028).</summary>
internal sealed record OnboardingDto
{
    public bool? Completed { get; init; }

    /// <summary>The marked options of step 1 (<c>touch</c>, <c>voice</c>, <c>noKeyboard</c>, <c>tremor</c>, <c>mouse</c>).</summary>
    public List<string>? Uses { get; init; }

    /// <summary>The marked option ids of step 2.</summary>
    public List<string>? Kit { get; init; }

    /// <summary>The settings step 1 changes, as the welcome left them.</summary>
    public WelcomeBaselineDto? Baseline { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
