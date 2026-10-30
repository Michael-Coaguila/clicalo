namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The header of the shortcuts column (ATJ-003).</summary>
/// <param name="Icon">The icon of the list.</param>
/// <param name="Title">Its name.</param>
/// <param name="Subtitle">[globalSub], «[opensWith] {proceso}» or [manualSub].</param>
/// <param name="CanEdit">Whether the edit button shows (not in Always visible).</param>
/// <param name="Editing">Whether the profile card is open.</param>
/// <param name="EditName">[editProf].</param>
/// <param name="AddText">[add].</param>
/// <param name="BackText">
/// «[backToProfile]»: the way back from the page of the editor to the list, in the narrow window (CCM-005).
/// </param>
public sealed record ListHeaderModel(
    string Icon,
    string Title,
    string Subtitle,
    bool CanEdit,
    bool Editing,
    string EditName,
    string AddText,
    string BackText = ""
);
