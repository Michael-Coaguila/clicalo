using Clicalo.Domain.Catalog;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// The four filter configurations of TAC-001 as <see cref="TouchSettings"/>: the three presets of
/// <c>data/catalogs/touch-presets.json</c> and a «Personal» one. The conversion from <see cref="TouchPreset"/> is the
/// caller's job in the product (Touch does not depend on Catalog), so the tests do it here.
/// </summary>
internal static class TouchPresetSettings
{
    /// <summary>Identifier of the custom values; not a catalog preset.</summary>
    public const string Personal = "personal";

    /// <summary>
    /// Custom values chosen inside the slider ranges of TAC-005, with the cancel distance switched off (0) so the
    /// tables also cover «Desactivado».
    /// </summary>
    public static TouchSettings PersonalSettings { get; } =
        new(TimeSpan.FromMilliseconds(450), 20, 0, TimeSpan.FromMilliseconds(120));

    /// <summary>The identifiers of the four configurations.</summary>
    public static IReadOnlyList<string> Ids { get; } =
    [.. TouchPresets.All.Select(preset => preset.Id), Personal];

    /// <summary>The identifiers of the four configurations, for theories.</summary>
    public static TheoryData<string> All => [.. Ids];

    /// <summary>The settings of <paramref name="id"/>.</summary>
    public static TouchSettings Get(string id) =>
        string.Equals(id, Personal, StringComparison.Ordinal)
            ? PersonalSettings
            : From(
                TouchPresets.Find(id)
                    ?? throw new ArgumentException("Unknown touch preset: " + id, nameof(id))
            );

    /// <summary>The conversion the caller of the recognizer does.</summary>
    public static TouchSettings From(TouchPreset preset) =>
        new(preset.Debounce, preset.HitSlopPx, preset.CancelMovePx, preset.MinContact);
}
