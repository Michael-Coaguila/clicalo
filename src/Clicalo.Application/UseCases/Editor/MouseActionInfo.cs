using Clicalo.Domain.Catalog;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>One of the eight mouse actions as <c>data/catalogs/mouse.json</c> names it (EDI-012).</summary>
/// <param name="Op">The action.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its name on the buttons («Desplazar ↑»).</param>
/// <param name="Spoken">Its name for screen readers when the label has a symbol («Desplazar hacia arriba»).</param>
public sealed record MouseActionInfo(
    MouseOp Op,
    IconRef Icon,
    LocalizedText Label,
    LocalizedText Spoken
);
