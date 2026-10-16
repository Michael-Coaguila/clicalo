namespace Clicalo.UI.Wpf.Controls;

/// <summary>What the message of a <see cref="StatusBar"/> says; each tone has its own icon, so it is never color alone.</summary>
public enum StatusTone
{
    /// <summary>A plain line, without icon («Listo. Toca un botón para usarlo.»).</summary>
    Neutral,

    /// <summary>A confirmation with ✓ in <c>success</c> (Cambios guardados, Ctrl+N enviado a Word).</summary>
    Success,

    /// <summary>Something to look at, with the warning icon in <c>warnText</c>.</summary>
    Warning,

    /// <summary>Something that failed, with the error icon in <c>dangerText</c>.</summary>
    Danger,
}
