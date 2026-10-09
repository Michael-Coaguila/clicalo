namespace Clicalo.Application.UseCases.Editor;

/// <summary>The warning under the combination box (EDI-007, docs/03 §7).</summary>
public enum ComboWarning
{
    /// <summary>Nothing to say.</summary>
    None,

    /// <summary>Windows does not let an app send it (Ctrl+Alt+Supr, Win+L): red, [blockedB]; the button does nothing.</summary>
    Blocked,

    /// <summary>Windows treats it specially (Win+G, Alt+Tab, Win+Tab, Ctrl+Shift+Esc): warn, [blockedS].</summary>
    Special,
}
