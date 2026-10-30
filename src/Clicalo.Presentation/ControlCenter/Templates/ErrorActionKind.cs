namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>The ways out of an AI error card (PLA-006).</summary>
public enum ErrorActionKind
{
    /// <summary>[Reintentar]: the same request again.</summary>
    Retry,

    /// <summary>[Usar mi clave] or [Cambiar clave]: the key field.</summary>
    Key,

    /// <summary>[Activar IA].</summary>
    Enable,

    /// <summary>[Perfil vacío], with the name already typed.</summary>
    Blank,

    /// <summary>[Ver plantillas].</summary>
    Templates,
}
