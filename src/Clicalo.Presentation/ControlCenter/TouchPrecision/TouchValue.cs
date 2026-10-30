namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>The four sliders of «Precisión táctil» (TAC-005), in order.</summary>
public enum TouchValue
{
    /// <summary>Anti doble toque: 0 to 1000 ms in steps of 50.</summary>
    Debounce,

    /// <summary>Área extra: 0 to 40 px in steps of 2.</summary>
    HitSlop,

    /// <summary>Cancelar si deslizas: 0 to 80 px in steps of 5.</summary>
    CancelMove,

    /// <summary>Contacto mínimo: 0 to 300 ms in steps of 10.</summary>
    MinContact,
}
