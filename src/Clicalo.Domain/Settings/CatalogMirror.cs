using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Settings;

/// <summary>
/// Values of <c>data/catalogs</c> the settings defaults and ranges need. The Settings module may only use Primitives
/// and Messages (<c>architecture/domain-modules.json</c>), so it cannot read the generated <c>Timings</c> and
/// <c>TouchPresets</c>; <c>SettingsSchemaTests</c> fails if any of these values drifts from the generated one. Once the
/// module may depend on Timing and Catalog (requested for the M2 integration), these mirrors give way to the generated
/// constants.
/// </summary>
internal static class CatalogMirror
{
    /// <summary><c>Timings.KeySafety.AutoReleaseDefault</c>.</summary>
    public static readonly TimeSpan AutoReleaseDefault = TimeSpan.FromSeconds(60);

    /// <summary><c>Timings.KeySafety.AutoReleaseChoices</c>.</summary>
    public static readonly ValueList<TimeSpan> AutoReleaseChoices =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(120),
    ];

    /// <summary><c>Timings.Ai.AiFreeDailyQuota</c>.</summary>
    public const int AiFreeDailyQuota = 5;

    /// <summary>Id of <c>TouchPresets.Default</c>.</summary>
    public const string DefaultTouchPreset = "mild-tremor";

    /// <summary>Debounce of <c>TouchPresets.Default</c>.</summary>
    public static readonly TimeSpan DefaultTouchDebounce = TimeSpan.FromMilliseconds(300);

    /// <summary>Hit slop of <c>TouchPresets.Default</c>, in logical pixels.</summary>
    public const int DefaultTouchHitSlopPx = 14;

    /// <summary>Cancel distance of <c>TouchPresets.Default</c>, in logical pixels.</summary>
    public const int DefaultTouchCancelMovePx = 35;

    /// <summary>Minimum contact of <c>TouchPresets.Default</c>.</summary>
    public static readonly TimeSpan DefaultTouchMinContact = TimeSpan.Zero;
}
