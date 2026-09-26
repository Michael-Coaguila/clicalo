namespace Clicalo.Domain.Keys;

/// <summary>
/// Modifier family of a key. The declaration order is the canonical send order of sticky modifiers:
/// Ctrl, Alt, Shift, Win (FIJ-006, EC-EJE-06).
/// </summary>
public enum ModifierKind
{
    /// <summary>Control.</summary>
    Ctrl,

    /// <summary>Alt, including AltGr (right Alt).</summary>
    Alt,

    /// <summary>Shift.</summary>
    Shift,

    /// <summary>Windows logo key.</summary>
    Win,
}
