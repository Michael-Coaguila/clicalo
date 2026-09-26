namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>What an InputProbe event means for S4 (only counted, never read).</summary>
internal enum ProbeSignal
{
    /// <summary>Nothing S4 counts.</summary>
    None,

    /// <summary><c>WM_KEYDOWN</c> or <c>WM_SYSKEYDOWN</c> of <c>VK_F24</c>: the internal chord reached the app.</summary>
    F24,

    /// <summary>A character message: text reached the app.</summary>
    Character,

    /// <summary><c>WM_SYSCOMMAND(SC_KEYMENU)</c>: the modifiers opened the menu bar.</summary>
    Menu,

    /// <summary>A Shift, Ctrl, Alt or Windows key message: recorded, not a failure (S4.md).</summary>
    ModifierKey,
}
