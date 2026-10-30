namespace Clicalo.Application.UseCases.Ai;

/// <summary>
/// The error card of «Crear con IA» (PLA-006): each one offers three ways out. Without the free quota (user decision
/// D5) there is no «limit»: a missing key is <see cref="NoKey"/> and the provider's limit is <see cref="BadKey"/>.
/// </summary>
public enum AiError
{
    /// <summary>No error.</summary>
    None,

    /// <summary>The AI is turned off: [Activar IA] · Perfil vacío · Ver plantillas.</summary>
    Off,

    /// <summary>No connection or no answer in 15 s: [Reintentar] · Perfil vacío · Ver plantillas.</summary>
    Offline,

    /// <summary>No key saved: [Usar mi clave] · Perfil vacío · Ver plantillas.</summary>
    NoKey,

    /// <summary>The key was refused or reached the provider's limit: [Cambiar clave] · Perfil vacío · Ver plantillas.</summary>
    BadKey,

    /// <summary>The provider failed: [Reintentar] · Perfil vacío · Ver plantillas.</summary>
    Unavailable,

    /// <summary>The answer was not valid: [Reintentar] · Perfil vacío · Ver plantillas.</summary>
    Invalid,
}
