namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The header of the shortcuts column (ATJ-003).</summary>
/// <param name="Icon">The icon of the list.</param>
/// <param name="Title">Its name.</param>
/// <param name="Subtitle">[globalSub], «[opensWith] {proceso}» or [manualSub].</param>
/// <param name="CanEdit">Whether the edit button shows (not in Always visible).</param>
/// <param name="Editing">Whether the profile card is open.</param>
/// <param name="EditName">[editProf].</param>
/// <param name="AddText">[add].</param>
public sealed record ListHeaderModel(
    string Icon,
    string Title,
    string Subtitle,
    bool CanEdit,
    bool Editing,
    string EditName,
    string AddText
);
