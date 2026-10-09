using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Keys;

/// <summary>
/// The visible names of a catalog key (<c>data/catalogs/keys.json</c>, R-04): product text loaded at run time, never
/// compiled in.
/// </summary>
/// <param name="Label">The full label («Ctrl der.», «Supr», «A»).</param>
/// <param name="Abbreviated">The abbreviation for the size S («Ctl», «⇧»), or <see langword="null"/> to use the label.</param>
/// <param name="Spoken">The name for screen readers of a glyph label («Flecha izquierda»), or <see langword="null"/>.</param>
public sealed record KeyLabel(
    LocalizedText Label,
    LocalizedText? Abbreviated,
    LocalizedText? Spoken
);
