namespace Clicalo.Application.Ports;

/// <summary>The states of the update card (ACT-001).</summary>
public enum UpdatePhase
{
    /// <summary>This copy was not installed with the installer (a development build): no updates.</summary>
    Unavailable,

    /// <summary>«Estás al día · v{x}» → [Buscar actualizaciones].</summary>
    UpToDate,

    /// <summary>«Buscando actualizaciones…», inactive.</summary>
    Checking,

    /// <summary>«Nueva versión disponible · v{x}» → [Instalar ahora].</summary>
    Found,

    /// <summary>«Instalando…» with its bar.</summary>
    Installing,

    /// <summary>«Actualizado · v{x}» → [Listo], after the restart into a new version.</summary>
    Updated,

    /// <summary>An error with its reason → [Reintentar].</summary>
    Failed,
}
