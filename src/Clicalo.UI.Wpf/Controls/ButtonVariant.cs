namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The four buttons of direction C (D12), and no others. All are bold (<c>label</c>), radius
/// <see cref="Theming.Generated.Radii.Medium"/>, and at least one control height high.
/// </summary>
public enum ButtonVariant
{
    /// <summary>The main action of a place: <c>accent</c> fill with <c>onAccent</c> text (Añadir, Probar, Empezar).</summary>
    Primary,

    /// <summary>Another action: <c>card</c> fill, <c>line</c> outline (≥ 3:1) and <c>text</c> (Duplicar, Atrás).</summary>
    Secondary,

    /// <summary>A quiet action: text in <c>accentInk</c> without fill or outline (Cambiar, Deshacer, Nuevo perfil).</summary>
    Tertiary,

    /// <summary>A destructive action: <c>card</c> fill, <c>line</c> outline and <c>dangerText</c> (Eliminar).</summary>
    Danger,
}
