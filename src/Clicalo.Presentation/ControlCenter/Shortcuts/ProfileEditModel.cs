using Clicalo.Presentation.ControlCenter.Editor;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The card «Editar perfil» (ATJ-004).</summary>
/// <param name="NameLabel">[profName].</param>
/// <param name="Name">The name.</param>
/// <param name="DictateName">The accessible name of the dictation button.</param>
/// <param name="IconLabel">[icon].</param>
/// <param name="Icon">The icon of the profile.</param>
/// <param name="ChangeIconName">[changeIcon]: the button that opens the icon picker.</param>
/// <param name="Picker">
/// The icon picker (D14), when open: the whole collection by category, with the 28 icons of a profile as its
/// suggestions (ATJ-004).
/// </param>
/// <param name="CompatTitle">[compatT].</param>
/// <param name="CompatText">[compatD].</param>
/// <param name="Compatible">The switch.</param>
/// <param name="ShareText">[shareProf].</param>
/// <param name="DoneText">[done].</param>
/// <param name="DeleteText">[delProf], or [delConfirm] when armed; null for General.</param>
/// <param name="DeleteArmed">Whether the first tap armed it.</param>
/// <param name="CanShare">Whether [Compartir] saves the profile as a file (DAT-007).</param>
/// <param name="ShareTextsText">[shareWithTexts] when the profile has texts to include in clear (PQ-37); else null.</param>
public sealed record ProfileEditModel(
    string NameLabel,
    string Name,
    string DictateName,
    string IconLabel,
    string Icon,
    string ChangeIconName,
    IconPickerModel? Picker,
    string CompatTitle,
    string CompatText,
    bool Compatible,
    string ShareText,
    string DoneText,
    string? DeleteText,
    bool DeleteArmed,
    bool CanShare = false,
    string? ShareTextsText = null
);
