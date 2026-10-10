using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Execution;

/// <summary>
/// The adjustable interaction times (ACC-006, WCAG 2.2.1): the confirmation windows (<c>ExecuteConfirmWindow</c>,
/// <c>DestructiveConfirmWindow</c>) and the duration of the notices last ×1, ×2 or ×3 as the person chose in General
/// (<c>UserSettings.TimeMultiplier</c>). Safety limits (the automatic release, the touch filter) never scale.
/// </summary>
public static class InteractionTime
{
    /// <summary><paramref name="duration"/> times <paramref name="multiplier"/>, which is kept inside ×1 to ×3.</summary>
    /// <param name="duration">The base duration, from <c>data/catalogs/timings.json</c>.</param>
    /// <param name="multiplier">The multiplier of the settings.</param>
    public static TimeSpan Scale(TimeSpan duration, int multiplier) =>
        duration * (int)SettingsSchema.TimeMultiplier.Clamp(multiplier);
}
