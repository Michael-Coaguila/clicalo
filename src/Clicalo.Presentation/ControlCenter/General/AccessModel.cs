namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>
/// «Accesibilidad y datos» (GEN-013, decision of the catalog): Reducir movimiento and Reiniciar Frecuentes, moved out of
/// «Seguridad de teclas».
/// </summary>
/// <param name="Caption">[secA11yData].</param>
/// <param name="ReduceMotion">[reduceM] (TEM-006).</param>
/// <param name="ResetTitle">[resetFreq], or [delConfirm] while the first tap is armed (REG-04).</param>
/// <param name="ResetName">[resetFreq], the accessible name.</param>
/// <param name="ResetDescription">[resetFreqD].</param>
/// <param name="ResetArmed">Whether the first tap armed it: danger fill.</param>
public sealed record AccessModel(
    string Caption,
    SwitchItem ReduceMotion,
    string ResetTitle,
    string ResetName,
    string ResetDescription,
    bool ResetArmed
);
