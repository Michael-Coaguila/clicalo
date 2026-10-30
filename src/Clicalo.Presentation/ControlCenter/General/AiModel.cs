using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>
/// «Inteligencia artificial» of General (GEN-015, PLA-003, PLA-004, ADR-0033): the AI on or off, the provider with where
/// its key comes from and, for Gemini, the notice of the free tier, the state of the consent with the button that
/// revokes it or gives it again, and the saved key of the provider with the button that deletes it in two taps. The key
/// is pasted in Plantillas, next to «Crear con IA».
/// </summary>
/// <param name="Caption">[secAi].</param>
/// <param name="Use">[aiUse]: on while the AI is not disabled.</param>
/// <param name="ProviderTitle">[aiProvider].</param>
/// <param name="Providers">[aiProviderAnthropic] and [aiProviderGemini], the chosen one selected.</param>
/// <param name="ProviderDetail">[aiProviderAnthropicD] or [aiProviderGeminiD] of the chosen provider.</param>
/// <param name="ProviderNotice">[aiGeminiFree] with Gemini; null with Anthropic.</param>
/// <param name="ConsentState">[consentGiven] or [consentNotGiven].</param>
/// <param name="Consent">Whether the consent is given.</param>
/// <param name="ConsentAction">[consentRevoke] or [consentGive].</param>
/// <param name="ConsentDetail">[consentD4]: the four data that are sent, and to which provider.</param>
/// <param name="KeyState">[quotaKey] or [keyNone].</param>
/// <param name="DeleteKeyText">[keyDelete], or [delConfirm] while armed; null without a saved key.</param>
/// <param name="DeleteKeyArmed">Whether the first tap armed the delete: danger fill (REG-04).</param>
/// <param name="UseKeyText">
/// [keyUse] without a saved key: it opens Plantillas with the field of the key, where the key is pasted; null with one.
/// </param>
public sealed record AiModel(
    string Caption,
    SwitchItem Use,
    string ProviderTitle,
    ValueList<SettingOption<AiProvider>> Providers,
    string ProviderDetail,
    string? ProviderNotice,
    string ConsentState,
    bool Consent,
    string ConsentAction,
    string ConsentDetail,
    string KeyState,
    string? DeleteKeyText,
    bool DeleteKeyArmed,
    string? UseKeyText = null
);
