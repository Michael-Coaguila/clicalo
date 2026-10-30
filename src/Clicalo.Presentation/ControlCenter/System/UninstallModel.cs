namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// The row «Desinstalar Clícalo» of «Inicio y estabilidad» (NFR-010, proposal P6): the data is kept unless
/// <see cref="DeleteData"/> is on, and the button takes two taps (REG-04).
/// </summary>
/// <param name="Title">[uninstallT].</param>
/// <param name="Description">[uninstallD], or [uninstallNotInstalled] for a copy that was not installed.</param>
/// <param name="DeleteData">[uninstallWipe] and [uninstallWipeD]: off by default.</param>
/// <param name="Button">[uninstallBtn], or [confirmB] while the first tap is armed.</param>
/// <param name="Armed">Whether the first tap is armed.</param>
/// <param name="Available">Whether this copy can be uninstalled from here (the installed one, and not busy).</param>
public sealed record UninstallModel(
    string Title,
    string Description,
    SwitchModel DeleteData,
    string Button,
    bool Armed,
    bool Available
);
