using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>The preview column of Plantillas (PLA-015 to PLA-017).</summary>
/// <param name="HasContent">Whether something is in preview.</param>
/// <param name="EmptyText">[pvEmpty].</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Name">Its name.</param>
/// <param name="Process">Its processes.</param>
/// <param name="UnknownText">[unknownMsg] for an unknown program.</param>
/// <param name="UnknownButton">[unknownBlank].</param>
/// <param name="InstalledNote">[instNoteAll] or [instNoteSome].</param>
/// <param name="KeyboardLine">The keyboard line.</param>
/// <param name="OnlyEsNote">[onlyEs].</param>
/// <param name="TextsNote">Texts left out of a shared profile.</param>
/// <param name="Rows">Its rows.</param>
/// <param name="ButtonText">The final button.</param>
/// <param name="ButtonIcon">Its icon.</param>
/// <param name="ButtonEnabled">Whether it works (N greater than 0, or Editar atajos).</param>
/// <param name="ButtonSecondary">Whether it is the secondary [editShortcuts].</param>
/// <param name="VariantText">
/// «Actualizar a la variante {idioma}» when installed shortcuts still have the keys of another programs language
/// (EC-PLA-04); null otherwise.
/// </param>
public sealed record PreviewModel(
    bool HasContent,
    string EmptyText,
    string Icon,
    string Name,
    string Process,
    string? UnknownText,
    string? UnknownButton,
    string? InstalledNote,
    string KeyboardLine,
    string? OnlyEsNote,
    string? TextsNote,
    ValueList<PreviewRowModel> Rows,
    string ButtonText,
    string ButtonIcon,
    bool ButtonEnabled,
    bool ButtonSecondary,
    string? VariantText = null
);
