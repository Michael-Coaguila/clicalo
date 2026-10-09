namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The identity of the shortcut in the editor (EDI-001 to EDI-003).</summary>
/// <param name="Icon">The icon of the preview tile.</param>
/// <param name="Category">Its color category.</param>
/// <param name="TileName">The name, or [namePh2].</param>
/// <param name="ChangeIconName">[changeIcon].</param>
/// <param name="NameLabel">[name].</param>
/// <param name="Name">The name as saved.</param>
/// <param name="Placeholder">[namePh].</param>
/// <param name="DictateName">[dictName].</param>
/// <param name="HintIcon">The icon of the hint line.</param>
/// <param name="Hint">[iconAuto] or [iconManual].</param>
/// <param name="PickerOpen">Whether the icon picker shows (the tile gets an accent border).</param>
public sealed record IdentityModel(
    string Icon,
    string Category,
    string TileName,
    string ChangeIconName,
    string NameLabel,
    string Name,
    string Placeholder,
    string DictateName,
    string HintIcon,
    string Hint,
    bool PickerOpen
);
