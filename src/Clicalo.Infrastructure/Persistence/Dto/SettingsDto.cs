using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>
/// The settings (DAT-001, docs/02 names). Every member is optional: a missing one takes its default from
/// <c>SettingsSchema.Defaults</c>, so an older document still loads.
/// </summary>
internal sealed record SettingsDto
{
    public string? Lang { get; init; }

    public string? Theme { get; init; }

    public string? Density { get; init; }

    public string? Size { get; init; }

    public int? Cols { get; init; }

    public int? RowsPref { get; init; }

    public int? TextScale { get; init; }

    public double? Opacity { get; init; }

    public bool? AutoDim { get; init; }

    public double? DimTo { get; init; }

    public bool? ShowKeys { get; init; }

    public bool? VoiceNumbers { get; init; }

    public bool? StickyModsRow { get; init; }

    public bool? ShowStripRow { get; init; }

    public bool? ShowTabsRow { get; init; }

    public bool? ReduceMotion { get; init; }

    public FeedbackDto? Feedback { get; init; }

    public bool? LockProfile { get; init; }

    public string? LastProfile { get; init; }

    public DockDto? Dock { get; init; }

    public List<MonitorPositionDto>? PanelPositions { get; init; }

    public TouchDto? Touch { get; init; }

    public KeySafetyDto? KeySafety { get; init; }

    public bool? AutoSuggestProfiles { get; init; }

    public KeyboardDto? Keyboard { get; init; }

    public AiDto? Ai { get; init; }

    public ReliabilityDto? Reliability { get; init; }

    public UpdatesDto? Updates { get; init; }

    public bool? NoKeyboardUser { get; init; }

    /// <summary>Settings of a later minor, kept when rewriting (§6.5).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
