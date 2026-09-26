using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>
/// The <c>payload</c> of <c>clicalo.json</c> and of every backup, schema 1.0 (blueprint §6.5, ADR-0007). Only
/// Infrastructure knows it: the mapper validates it, repairs what can be repaired and builds the Domain document.
/// </summary>
internal sealed record PayloadDto
{
    /// <summary>Every setting (DAT-001).</summary>
    public SettingsDto? Settings { get; init; }

    /// <summary>The Always visible row.</summary>
    public ShortcutListDto? Always { get; init; }

    /// <summary>The profiles, General included, in order.</summary>
    public List<ProfileDto>? Profiles { get; init; }

    /// <summary>Pins, hidden and the usage epoch of Frequents (the usage itself lives in <c>usage.json</c>).</summary>
    public FrequentsDto? Frequents { get; init; }

    /// <summary>Repeated combinations marked «It's fine», as stable canonical strings (REP-*).</summary>
    public List<string>? DupIgnored { get; init; }

    /// <summary>The welcome state.</summary>
    public OnboardingDto? Onboarding { get; init; }

    /// <summary>
    /// Only in backups: the usage of that moment (§6.8), keyed by shortcut id in ordinal order, in Unix milliseconds.
    /// </summary>
    public Dictionary<string, List<long>>? Usage { get; init; }

    /// <summary>Fields of a later minor, kept when rewriting (§6.5).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
