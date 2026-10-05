namespace Clicalo.Domain.Settings;

/// <summary>
/// The touch filter chosen by the user (TAC-001, docs/02 <c>touch</c>): a preset of
/// <c>data/catalogs/touch-presets.json</c> or «personal» values. A zero disables each check (TAC-005).
/// </summary>
/// <param name="Preset">The preset id, or <c>personal</c>.</param>
/// <param name="Debounce">A second accepted touch on the same button within this time is ignored.</param>
/// <param name="HitSlopPx">Extra hit area around each button, in logical pixels.</param>
/// <param name="CancelMovePx">A contact that moves further is not a tap, in logical pixels.</param>
/// <param name="MinContact">Shorter contacts are ignored.</param>
public sealed record TouchFilterSettings(
    string Preset,
    TimeSpan Debounce,
    int HitSlopPx,
    int CancelMovePx,
    TimeSpan MinContact
);
