using Clicalo.Domain.Messages;
using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.ControlCenter;

/// <summary>
/// What General › IA and the key field of Plantillas say of each AI provider (ADR-0033): its name, where its key comes
/// from and, for Gemini, the notice of the free tier.
/// </summary>
internal static class AiProviderTexts
{
    /// <summary>The providers in the order they are offered: Anthropic, the provider before 1.2, first.</summary>
    public static IReadOnlyList<AiProvider> All { get; } = Enum.GetValues<AiProvider>();

    /// <summary>[aiProviderAnthropic] or [aiProviderGemini], also the {provider} of [keyNone], [errNoKeyD] and [consentD4].</summary>
    /// <param name="provider">The provider.</param>
    public static Message Name(AiProvider provider) =>
        provider == AiProvider.Gemini ? L.AiProviderGemini : L.AiProviderAnthropic;

    /// <summary>[aiProviderAnthropicD] or [aiProviderGeminiD]: where the key comes from.</summary>
    /// <param name="provider">The provider.</param>
    public static Message Detail(AiProvider provider) =>
        provider == AiProvider.Gemini ? L.AiProviderGeminiD : L.AiProviderAnthropicD;

    /// <summary>[aiGeminiFree] for Gemini: on the free tier Google may use and read what is sent; null otherwise.</summary>
    /// <param name="provider">The provider.</param>
    public static Message? Notice(AiProvider provider) =>
        provider == AiProvider.Gemini ? L.AiGeminiFree : null;
}
