namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// An action the guide strip offers as an extra large button while a step is current, so the maintainer never has
/// to touch an activatable window in the middle of a cycle.
/// </summary>
internal enum StepAction
{
    /// <summary>No extra button.</summary>
    None,

    /// <summary>«Forzar activación del panel»: <c>SetForegroundWindow</c> on the panel without a lease (S1 row 31).</summary>
    ForceActivation,

    /// <summary>«Aviso cortés»: a polite live announcement (S3 row 9a).</summary>
    PoliteNotice,

    /// <summary>«Aviso urgente»: an assertive live announcement (S3 row 9b).</summary>
    AssertiveNotice,

    /// <summary>
    /// «Activar números de Clícalo» or «Quitar números de Clícalo»: the voice numbers of the panel (S3 row 6), from the
    /// strip, so the maintainer never touches the activatable control window in the middle of the row.
    /// </summary>
    ToggleVoiceNumbers,
}
