namespace Clicalo.Domain.Keys;

/// <summary>
/// The modifiers of a <see cref="CanonicalChord"/> as a set, including their side (REP-001): Ctrl, left Ctrl and right
/// Ctrl are different members.
/// </summary>
[Flags]
public enum ChordModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>Ctrl on either side.</summary>
    Ctrl = 1 << 0,

    /// <summary>Left Ctrl.</summary>
    LeftCtrl = 1 << 1,

    /// <summary>Right Ctrl.</summary>
    RightCtrl = 1 << 2,

    /// <summary>Alt on either side.</summary>
    Alt = 1 << 3,

    /// <summary>Left Alt.</summary>
    LeftAlt = 1 << 4,

    /// <summary>Right Alt (AltGr in layouts that have it).</summary>
    RightAlt = 1 << 5,

    /// <summary>Shift on either side.</summary>
    Shift = 1 << 6,

    /// <summary>Left Shift.</summary>
    LeftShift = 1 << 7,

    /// <summary>Right Shift.</summary>
    RightShift = 1 << 8,

    /// <summary>Windows key on either side.</summary>
    Win = 1 << 9,

    /// <summary>Left Windows key.</summary>
    LeftWin = 1 << 10,

    /// <summary>Right Windows key.</summary>
    RightWin = 1 << 11,
}
