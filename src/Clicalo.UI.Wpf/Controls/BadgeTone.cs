namespace Clicalo.UI.Wpf.Controls;

/// <summary>The fill of a <see cref="Badge"/>; each color keeps its one meaning.</summary>
public enum BadgeTone
{
    /// <summary>A plain label: <c>card</c> fill and <c>muted</c> text (Incompleto, the version).</summary>
    Neutral,

    /// <summary>Something interactive or chosen: <c>accent</c> fill and <c>onAccent</c> text (Nuevo, Activo).</summary>
    Accent,

    /// <summary>Attention: <c>warn</c> fill and <c>onWarn</c> text (Repetida).</summary>
    Warn,
}
