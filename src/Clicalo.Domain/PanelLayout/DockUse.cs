namespace Clicalo.Domain.PanelLayout;

/// <summary>What a button of the open bar just did, for its automatic collapse (PES-012).</summary>
public enum DockUse
{
    /// <summary>An action ran (a tap, a text, a macro, a web or an app, Repeat).</summary>
    Ran,

    /// <summary>A Mantener was released.</summary>
    HoldReleased,

    /// <summary>An Alternar was latched or released.</summary>
    ToggleChanged,

    /// <summary>The first tap of a shortcut that asks for confirmation armed it.</summary>
    ConfirmArmed,
}
