using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The card «Editar perfil» (ATJ-004).</summary>
/// <param name="NameLabel">[profName].</param>
/// <param name="Name">The name.</param>
/// <param name="DictateName">The accessible name of the dictation button.</param>
/// <param name="IconLabel">[icon].</param>
/// <param name="Icons">The current icon and the 28 of a profile.</param>
/// <param name="CompatTitle">[compatT].</param>
/// <param name="CompatText">[compatD].</param>
/// <param name="Compatible">The switch.</param>
/// <param name="ShareText">[shareProf].</param>
/// <param name="DoneText">[done].</param>
/// <param name="DeleteText">[delProf], or [delConfirm] when armed; null for General.</param>
/// <param name="DeleteArmed">Whether the first tap armed it.</param>
public sealed record ProfileEditModel(
    string NameLabel,
    string Name,
    string DictateName,
    string IconLabel,
    ValueList<IconOption> Icons,
    string CompatTitle,
    string CompatText,
    bool Compatible,
    string ShareText,
    string DoneText,
    string? DeleteText,
    bool DeleteArmed
);
