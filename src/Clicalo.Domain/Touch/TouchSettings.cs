using System.Runtime.InteropServices;

namespace Clicalo.Domain.Touch;

/// <summary>
/// The four values of the touch filter (TAC-001, TAC-002), in the units the user edits: the active preset of
/// <c>data/catalogs/touch-presets.json</c> (generated as <c>Clicalo.Domain.Catalog.TouchPresets</c>) or the custom
/// values of the settings. Distances are logical pixels; <see cref="GestureRecognizer"/> scales them to physical
/// pixels with the DPI scale of its surface. Blueprint §7.8 calls it <c>TouchParams</c>.
/// </summary>
/// <param name="Debounce">A second accepted touch on the same target within this time is ignored (TAC-002 step 3).</param>
/// <param name="HitSlopPx">Extra hit area around each target, in logical pixels (TAC-002, REG-02).</param>
/// <param name="CancelMovePx">A contact that moves further than this is not a tap, in logical pixels (TAC-002 step 1).</param>
/// <param name="MinContact">Contacts shorter than this are ignored; zero disables the check (TAC-002 step 2).</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TouchSettings(
    TimeSpan Debounce,
    int HitSlopPx,
    int CancelMovePx,
    TimeSpan MinContact
);
