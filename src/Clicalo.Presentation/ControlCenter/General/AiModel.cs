namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>
/// «Inteligencia artificial» of General (GEN-015, PLA-003, PLA-004): the AI on or off, the state of the consent with
/// the button that revokes it or gives it again, and the saved key with the button that deletes it in two taps. The key
/// is pasted in Plantillas, next to «Crear con IA».
/// </summary>
/// <param name="Caption">[secAi].</param>
/// <param name="Use">[aiUse]: on while the AI is not disabled.</param>
/// <param name="ConsentState">[consentGiven] or [consentNotGiven].</param>
/// <param name="Consent">Whether the consent is given.</param>
/// <param name="ConsentAction">[consentRevoke] or [consentGive].</param>
/// <param name="ConsentDetail">[consentD4]: the four data that are sent.</param>
/// <param name="KeyState">[quotaKey] or [keyNone].</param>
/// <param name="DeleteKeyText">[keyDelete], or [delConfirm] while armed; null without a saved key.</param>
/// <param name="DeleteKeyArmed">Whether the first tap armed the delete: danger fill (REG-04).</param>
public sealed record AiModel(
    string Caption,
    SwitchItem Use,
    string ConsentState,
    bool Consent,
    string ConsentAction,
    string ConsentDetail,
    string KeyState,
    string? DeleteKeyText,
    bool DeleteKeyArmed
);
