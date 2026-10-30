namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>
/// The editor column of a list without shortcuts: the empty state of the panel (CUA-010) instead of [pickOne], which
/// asked to choose something that does not exist, with the two ways to fill the list.
/// </summary>
/// <param name="Title">[emptyProfT].</param>
/// <param name="Text">[emptyProfS] with the name of the list.</param>
/// <param name="AddText">[addShortcut]: opens «Añadir atajo».</param>
/// <param name="TemplatesText">[seeTpl]: opens Plantillas.</param>
public sealed record EmptyListModel(
    string Title,
    string Text,
    string AddText,
    string TemplatesText
);
