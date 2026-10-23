namespace Clicalo.Domain.Settings;

/// <summary>The provider of «Crear con IA», always with the person's own key (ADR-0014, ADR-0033, user decision D5).</summary>
public enum AiProvider
{
    /// <summary>Claude by Anthropic: the only provider before schema 1.2, and the default.</summary>
    Anthropic,

    /// <summary>Gemini by Google, with a Google AI Studio key; on the free tier Google may use what is sent.</summary>
    Gemini,
}
