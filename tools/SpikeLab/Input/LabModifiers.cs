namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>
/// Modifiers of a laboratory chord. They are always sent as the LEFT key: the laboratory never injects AltGr, right
/// Ctrl or any right-hand modifier (dictation tools hook them on the maintainer's machine).
/// </summary>
[Flags]
internal enum LabModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>Left Shift.</summary>
    Shift = 1 << 0,

    /// <summary>Left Ctrl.</summary>
    Control = 1 << 1,

    /// <summary>Left Alt.</summary>
    Alt = 1 << 2,

    /// <summary>Left Windows.</summary>
    Windows = 1 << 3,
}
