using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>What «Precisión táctil» shows (docs/05 §4, TAC-005, TAC-006).</summary>
/// <param name="Title">[touchTitle].</param>
/// <param name="Subtitle">[touchSub].</param>
/// <param name="Presets">Estándar, Temblor leve, Temblor fuerte and Personal.</param>
/// <param name="Sliders">The four values of the filter.</param>
/// <param name="Test">The test zone.</param>
public sealed record TouchScreen(
    string Title,
    string Subtitle,
    ValueList<PresetOption> Presets,
    ValueList<TouchSlider> Sliders,
    TestZoneModel Test
);
