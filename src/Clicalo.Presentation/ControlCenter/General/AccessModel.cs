using Clicalo.Domain.Primitives;

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
/// <param name="TimesTitle">[timeMultiplierT] (ACC-006).</param>
/// <param name="TimesDescription">[timeMultiplierD].</param>
/// <param name="Times">×1, ×2 and ×3.</param>
/// <param name="Hotkey">[globalHotkeyT]: the global shortcut that shows or hides the panel, off by default (BUR-005, D10).</param>
/// <param name="HotkeyChoicesName">[keys], the accessible name of the list of combinations.</param>
/// <param name="Hotkeys">The closed list of combinations while the shortcut is on; empty while it is off.</param>
public sealed record AccessModel(
    string Caption,
    SwitchItem ReduceMotion,
    string ResetTitle,
    string ResetName,
    string ResetDescription,
    bool ResetArmed,
    string TimesTitle,
    string TimesDescription,
    ValueList<SettingOption<int>> Times,
    SwitchItem Hotkey,
    string HotkeyChoicesName,
    ValueList<SettingOption<string>> Hotkeys
);
