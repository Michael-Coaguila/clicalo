namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// The tab «Inicio y estabilidad» (SIS-002 as modified by D7): «Iniciar con Windows», «Reabrir como administrador» and
/// «Recuperación automática», which the guardian always does, and «Desinstalar Clícalo» (NFR-010, P6). «Una sola
/// ventana» is not built (proposal P2).
/// </summary>
/// <param name="StartWithWindows">«Iniciar con Windows».</param>
/// <param name="Admin">«Reabrir como administrador».</param>
/// <param name="Crash">[rCrash].</param>
/// <param name="CrashDescription">The aligned description of the recovery.</param>
/// <param name="CrashStatus">[alwaysOn].</param>
/// <param name="Uninstall">«Desinstalar Clícalo».</param>
public sealed record StartModel(
    SwitchModel StartWithWindows,
    AdminRowModel Admin,
    string Crash,
    string CrashDescription,
    string CrashStatus,
    UninstallModel Uninstall
);
