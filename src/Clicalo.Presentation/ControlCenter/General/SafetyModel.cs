using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>«Seguridad de teclas» (GEN-012).</summary>
/// <param name="Caption">[secSafety].</param>
/// <param name="MaxHoldTitle">[safeMax].</param>
/// <param name="MaxHold">30 s, 1 min, 2 min and Nunca (<see langword="null"/>).</param>
/// <param name="MaxHoldDescription">[safeMaxD].</param>
/// <param name="ReleaseOnAppSwitch">[safeSwitch].</param>
public sealed record SafetyModel(
    string Caption,
    string MaxHoldTitle,
    ValueList<SettingOption<TimeSpan?>> MaxHold,
    string MaxHoldDescription,
    SwitchItem ReleaseOnAppSwitch
);
