namespace Clicalo.UI.Wpf.Controls;

/// <summary>The fills of the prototype's buttons, chips and option tiles (all from theme tokens).</summary>
public enum ButtonAppearance
{
    /// <summary>Main action: <c>accent</c> fill with <c>onAccent</c> text (Crear perfil, Añadir atajo, Deshacer).</summary>
    Accent,

    /// <summary>Secondary action: <c>cardHi</c> fill with <c>text</c> (Opacidad − / +, Repetir).</summary>
    Neutral,

    /// <summary>Quiet action: no fill and a <c>border</c> outline (Ahora no).</summary>
    Outline,

    /// <summary>Header action: no fill and no outline (Buscar, Editar, Ajustes rápidos, Minimizar).</summary>
    Ghost,

    /// <summary>Destructive action: <c>danger</c> fill with <c>onDanger</c> text (Eliminar armado, Soltar todo).</summary>
    Danger,

    /// <summary>Warning action: <c>warn</c> fill with <c>onWarn</c> text (Activar modo administrador).</summary>
    Warn,
}
