namespace Clicalo.Domain.Catalog;

/// <summary>
/// A preset of the touch filter (TAC-001), generated into <see cref="TouchPresets"/> from
/// <c>data/catalogs/touch-presets.json</c>.
/// </summary>
/// <param name="Id">Stable preset identifier, persisted in the settings.</param>
/// <param name="Debounce">A second accepted touch on the same button within this time is ignored.</param>
/// <param name="HitSlopPx">Extra hit area around each button, in logical pixels.</param>
/// <param name="CancelMovePx">A contact that moves further than this is not a tap, in logical pixels.</param>
/// <param name="MinContact">Contacts shorter than this are ignored.</param>
public sealed record TouchPreset(
    string Id,
    TimeSpan Debounce,
    int HitSlopPx,
    int CancelMovePx,
    TimeSpan MinContact
);
